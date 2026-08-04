using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupExpenseCommandHandler : IRequestHandler<CreateNonGroupExpenseCommand, Result<CreateExpenseResponse>>
{
    private const string NonGroupExpensesUrl = "/shared/nongroup/expenses";

    private readonly IExpensesRepository _expensesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;
    private readonly NotificationService _notificationService;

    public CreateNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        IUsersRepository usersRepository,
        ValidationService validationService,
        UserLabelService userLabelService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _userLabelService = userLabelService;
        _notificationService = notificationService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateNonGroupExpenseCommand command, CancellationToken ct)
    {
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

        var now = DateTime.UtcNow;

        var addLabelsToGroupResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = Guid.NewGuid().ToString();

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
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(newNonGroupExpense, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        await NotifyParticipants(command, ct);

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }

    /// <summary>
    /// Tells everyone dragged into this expense that it happened. Delivery runs in the background,
    /// so a push outage can never fail an expense that is already written.
    /// </summary>
    private async Task NotifyParticipants(CreateNonGroupExpenseCommand command, CancellationToken ct)
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
            NonGroupExpensesUrl,
            ct);
    }
}