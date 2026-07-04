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
    private readonly PushNotificationService _pushNotificationService;

    public CreateExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        ValidationService validationService,
        GroupService groupService,
        PushNotificationService pushNotificationService)
    {
        _expensesRepository = expensesRepository;
        _validationService = validationService;
        _groupService = groupService;
        _permissionService = permissionService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateExpenseResponse>();
        }

        var (_, group, memberId) = permissionResult.Value;

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

        var expenseId = Guid.NewGuid().ToString();

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
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(newExpense, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        var (user, _, _) = permissionResult.Value;

        // Only notify group members that are part of the expense, not the whole group
        var participantMemberIds = command.Payments.Select(x => x.MemberId)
            .Concat(command.Shares.Select(x => x.MemberId))
            .ToHashSet();

        var participantUserIds = group.Members
            .Where(m => participantMemberIds.Contains(m.Id) && m.UserId != command.UserId)
            .Select(m => m.UserId);

        _pushNotificationService.NotifyInBackground(
            participantUserIds,
            group.Name,
            $"{user.Username} added \"{command.Description}\" ({command.Amount} {command.Currency}).",
            $"/shared/{command.GroupId}/expenses");

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }
}