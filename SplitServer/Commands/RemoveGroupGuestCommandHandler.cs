using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class RemoveGroupGuestCommandHandler : IRequestHandler<RemoveGroupGuestCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public RemoveGroupGuestCommandHandler(
        PermissionService permissionService,
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        IInvitationsRepository invitationsRepository)
    {
        _permissionService = permissionService;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result> Handle(RemoveGroupGuestCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        var guestToRemove = group.Guests.FirstOrDefault(g => g.Id == command.GuestId);

        if (guestToRemove is null)
        {
            return Result.Failure<Result>("This guest does not exist in this group");
        }

        var existsInAnyExpense = await _expensesRepository.ExistsInAnyExpense(command.GroupId, command.GuestId, ct);
        var existsInAnyTransfer = await _transfersRepository.ExistsInAnyTransfer(command.GroupId, command.GuestId, ct);

        if (existsInAnyExpense || existsInAnyTransfer)
        {
            return Result.Failure<Result>("This guest has group activity");
        }

        var editedGroup = group with
        {
            Guests = group.Guests.Where(g => g.Id != command.GuestId).ToList(),
            Updated = DateTime.UtcNow
        };

        var updateGroupResult = await _groupsRepository.Update(editedGroup, ct);

        if (updateGroupResult.IsFailure)
        {
            return updateGroupResult;
        }

        return await _invitationsRepository.DeleteByGuestId(command.GuestId, group.Id, ct);
    }
}