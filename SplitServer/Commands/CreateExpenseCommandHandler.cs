using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly GroupService _groupService;
    private readonly NotificationService _notificationService;

    public CreateExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        GroupService groupService,
        NotificationService notificationService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _groupService = groupService;
        _permissionService = permissionService;
        _notificationService = notificationService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateExpenseResponse>();
        }

        var (user, group, memberId) = permissionResult.Value;

        var expenseIdResult = ClientIds.Resolve(command.ExpenseId);

        if (expenseIdResult.IsFailure)
        {
            return expenseIdResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = expenseIdResult.Value;

        if (command.ExpenseId is not null)
        {
            var isStoredResult =
                await ClientIds.IsAlreadyStored<Expense, GroupExpense>(_expensesRepository, expenseId, memberId, ct);

            if (isStoredResult.IsFailure)
            {
                return isStoredResult.ConvertFailure<CreateExpenseResponse>();
            }

            // A resend of an expense that is already in. It gets the answer the first send would
            // have had, and the people on it are not told about it a second time.
            if (isStoredResult.Value)
            {
                return new CreateExpenseResponse { ExpenseId = expenseId };
            }
        }

        var expenseValidationResult =
            _validationService.ValidateExpense(group, command.Payments, command.Shares, command.Amount, command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var now = DateTime.UtcNow;

        var labelsWithIds = GroupService.CreateLabelsWithIds(command.Labels, group.Labels);

        var addLabelsToGroupResult = await _groupService.AddLabelsToGroupIfMissing(group, labelsWithIds, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var newExpense = new GroupExpense
        {
            Id = expenseId,
            Created = now,
            Updated = now,
            GroupId = command.GroupId,
            CreatorId = memberId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = labelsWithIds.Select(x => x.Id).ToList(),
            Location = command.Location,
            RecurringExpenseId = command.RecurringExpenseId
        };

        var writeResult = await _expensesRepository.Insert(newExpense, ct);

        if (writeResult.IsFailure)
        {
            // Two sends of one expense can both get past the check above. The later insert then
            // fails on the duplicate id, and the expense it carries is already stored.
            if (command.ExpenseId is not null)
            {
                var isStoredResult =
                    await ClientIds.IsAlreadyStored<Expense, GroupExpense>(_expensesRepository, expenseId, memberId, ct);

                if (isStoredResult.IsSuccess && isStoredResult.Value)
                {
                    return new CreateExpenseResponse { ExpenseId = expenseId };
                }
            }

            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        await NotifyInvolvedMembers(command, newExpense, group, user.Username, ct);

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }

    /// <summary>
    /// Notifies only the members carrying a payment or a share, never the whole group: someone the
    /// expense does not involve has no reason to hear about it. Delivery runs in the background, so
    /// a push outage can never fail an expense that is already written.
    /// </summary>
    private async Task NotifyInvolvedMembers(
        CreateExpenseCommand command,
        GroupExpense expense,
        Group group,
        string creatorUsername,
        CancellationToken ct)
    {
        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            command.Payments.Select(x => x.MemberId).Concat(command.Shares.Select(x => x.MemberId)),
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{creatorUsername} added \"{command.Description}\" ({command.Amount} {command.Currency})",
            ExpenseLinks.JumpTo(expense),
            ct);
    }
}