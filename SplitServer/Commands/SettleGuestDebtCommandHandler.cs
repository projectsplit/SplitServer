using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class SettleGuestDebtCommandHandler : IRequestHandler<SettleGuestDebtCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly DebtService _debtService;

    public SettleGuestDebtCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        DebtService debtService,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _debtService = debtService;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result> Handle(SettleGuestDebtCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("You are not a member of this group");
        }

        if (group.Guests.All(x => x.Id != command.GuestId))
        {
            return Result.Failure("Guest is not a member of this group");
        }

        var debts = await _debtService.GetDebts(group.Id, ct);

        var now = DateTime.UtcNow;

        var transfers = debts
            .Where(x => x.Debtor == command.GuestId)
            .Select(
                x => new Transfer
                {
                    Id = Guid.NewGuid().ToString(),
                    IsDeleted = false,
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