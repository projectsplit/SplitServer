using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeleteTransferCommandHandler : IRequestHandler<DeleteTransferCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;

    public DeleteTransferCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _transfersRepository = transfersRepository;
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

        var groupMaybe = await _groupsRepository.GetById(transfer.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {transfer.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != command.UserId))
        {
            return Result.Failure("User must be a group member");
        }

        return await _transfersRepository.SoftDelete(command.TransferId, ct);
    }
}