using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupExpenseCommandHandler : IRequestHandler<CreateNonGroupExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;
    private readonly ConnectionService _connectionService;
    private readonly NotificationService _notificationService;

    public CreateNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        IUsersRepository usersRepository,
        ValidationService validationService,
        UserLabelService userLabelService,
        ConnectionService connectionService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _userLabelService = userLabelService;
        _connectionService = connectionService;
        _notificationService = notificationService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateNonGroupExpenseCommand command, CancellationToken ct)
    {
        var expenseIdResult = ClientIds.Resolve(command.ExpenseId);

        if (expenseIdResult.IsFailure)
        {
            return expenseIdResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = expenseIdResult.Value;

        // Ahead of validation: a resend of an expense that is already in has to succeed even if the
        // people on it have since stopped being connected.
        if (command.ExpenseId is not null)
        {
            var isStoredResult =
                await ClientIds.IsAlreadyStored<Expense, NonGroupExpense>(_expensesRepository, expenseId, command.UserId, ct);

            if (isStoredResult.IsFailure)
            {
                return isStoredResult.ConvertFailure<CreateExpenseResponse>();
            }

            if (isStoredResult.Value)
            {
                return new CreateExpenseResponse { ExpenseId = expenseId };
            }
        }

        var expenseValidationResult = _validationService.ValidateNonGroupExpense(
            command.Payments,
            command.Shares,
            command.Amount,
            command.Currency,
            command.UserId);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var participantUserIds = command.Payments.Select(x => x.UserId)
            .Concat(command.Shares.Select(x => x.UserId))
            .ToList();

        var connectionResult = await _connectionService.VerifyCanSplitWith(command.UserId, participantUserIds, ct);

        if (connectionResult.IsFailure)
        {
            return connectionResult.ConvertFailure<CreateExpenseResponse>();
        }

        var now = DateTime.UtcNow;

        var addLabelsToGroupResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var newNonGroupExpense = new NonGroupExpense
        {
            Id = expenseId,
            Created = now,
            Updated = now,
            CreatorId = command.UserId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location,
            RecurringExpenseId = command.RecurringExpenseId
        };

        var writeResult = await _expensesRepository.Insert(newNonGroupExpense, ct);

        if (writeResult.IsFailure)
        {
            // Two sends of one expense can both get past the check above. The later insert then
            // fails on the duplicate id, and the expense it carries is already stored.
            if (command.ExpenseId is not null)
            {
                var isStoredResult =
                    await ClientIds.IsAlreadyStored<Expense, NonGroupExpense>(_expensesRepository, expenseId, command.UserId, ct);

                if (isStoredResult.IsSuccess && isStoredResult.Value)
                {
                    return new CreateExpenseResponse { ExpenseId = expenseId };
                }
            }

            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        await NotifyParticipants(command, newNonGroupExpense, ct);

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }

    /// <summary>
    /// Tells everyone dragged into this expense that it happened. Delivery runs in the background,
    /// so a push outage can never fail an expense that is already written.
    /// </summary>
    private async Task NotifyParticipants(
        CreateNonGroupExpenseCommand command,
        NonGroupExpense expense,
        CancellationToken ct)
    {
        var recipientUserIds = command.Payments.Select(x => x.UserId)
            .Concat(command.Shares.Select(x => x.UserId))
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();
        
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        await _notificationService.Notify(
            recipientUserIds,
            "New expense",
            $"{creatorUsername} added \"{command.Description}\" ({command.Amount} {command.Currency}) with you",
            ExpenseLinks.JumpTo(expense),
            ct);
    }
}