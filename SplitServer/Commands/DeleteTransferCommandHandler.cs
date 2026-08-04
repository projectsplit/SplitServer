using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class DeleteTransferCommandHandler : IRequestHandler<DeleteTransferCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly NotificationService _notificationService;

    public DeleteTransferCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ITransfersRepository transfersRepository,
        NotificationService notificationService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _transfersRepository = transfersRepository;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(DeleteTransferCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var transferMaybe = await _transfersRepository.GetById(command.TransferId, ct);

        if (transferMaybe.HasNoValue)
        {
            return Result.Failure($"Transfer with id {command.TransferId} was not found");
        }

        var transfer = transferMaybe.Value;

        if (transfer is not GroupTransfer groupTransfer)
        {
            return Result.Failure($"Transfer with id {command.TransferId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(groupTransfer.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {groupTransfer.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("User must be a group member");
        }

        var deleteResult = await _transfersRepository.Delete(command.TransferId, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            [groupTransfer.SenderId, groupTransfer.ReceiverId],
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{userMaybe.Value.Username} deleted a transfer of {groupTransfer.Amount} {groupTransfer.Currency}",
            $"/shared/{group.Id}/transfers",
            ct);

        return deleteResult;
    }
}