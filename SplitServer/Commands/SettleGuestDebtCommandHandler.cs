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

    public SettleGuestDebtCommandHandler(
        ITransfersRepository transfersRepository,
        PermissionService permissionService,
        IExpensesRepository expensesRepository)
    {
        _transfersRepository = transfersRepository;
        _permissionService = permissionService;
        _expensesRepository = expensesRepository;
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

        var groupExpenses = await _expensesRepository.GetAllByGroupId(group.Id, ct);
        var groupTransfers = await _transfersRepository.GetAllByGroupId(group.Id, ct);

        var debts = GroupService.GetDebts(group, groupExpenses, groupTransfers);

        var now = DateTime.UtcNow;

        var transfers = debts
            .Where(x => x.Debtor == command.GuestId)
            .Select(
                x => new Transfer
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

        return await _transfersRepository.InsertMany(transfers, ct);
    }
}