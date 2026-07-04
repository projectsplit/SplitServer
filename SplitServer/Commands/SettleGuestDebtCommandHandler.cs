using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class SettleGuestDebtCommandHandler : IRequestHandler<SettleGuestDebtCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly PushNotificationService _pushNotificationService;

    public SettleGuestDebtCommandHandler(
        ITransfersRepository transfersRepository,
        PermissionService permissionService,
        IExpensesRepository expensesRepository,
        PushNotificationService pushNotificationService)
    {
        _transfersRepository = transfersRepository;
        _permissionService = permissionService;
        _expensesRepository = expensesRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result> Handle(SettleGuestDebtCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        if (group.Guests.All(x => x.Id != command.GuestId))
        {
            return Result.Failure("Guest is not a member of this group");
        }

        var groupExpenses = await _expensesRepository.GetGroupExpensesByGroupId(group.Id, ct);
        var groupTransfers = await _transfersRepository.GetAllByGroupId(group.Id, ct);

        var debts = GroupService.GetDebts(groupExpenses, groupTransfers);

        var now = DateTime.UtcNow;

        var transfers = debts
            .Where(x => x.Debtor == command.GuestId)
            .Select(x => new GroupTransfer
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                GroupId = command.GroupId,
                CreatorId = group.Members.Single(m => m.UserId == command.UserId).Id,
                SenderId = x.Debtor,
                ReceiverId = x.Creditor,
                Amount = x.Amount,
                Currency = x.Currency,
                Description = "Guest settle",
                Occurred = now
            })
            .ToList();

        var writeResult = await _transfersRepository.InsertMany(transfers, ct);

        if (writeResult.IsFailure)
        {
            return writeResult;
        }

        var (user, _, _) = permissionResult.Value;

        var guestName = group.Guests.Single(x => x.Id == command.GuestId).Name;

        var creditorMemberIds = transfers.Select(x => x.ReceiverId).ToHashSet();

        var creditorUserIds = group.Members
            .Where(m => creditorMemberIds.Contains(m.Id) && m.UserId != command.UserId)
            .Select(m => m.UserId);

        _pushNotificationService.NotifyInBackground(
            creditorUserIds,
            group.Name,
            $"{user.Username} settled \"{guestName}\"'s debts.",
            $"/shared/{command.GroupId}/debts");

        return Result.Success();
    }
}