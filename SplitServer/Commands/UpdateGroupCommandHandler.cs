using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public UpdateGroupCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(UpdateGroupCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(command.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure($"Group with id {command.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.OwnerId != user.Id)
        {
            return Result.Failure("This group does not belong to user");
        }

        var updatedGroup = group with
        {
            Name = command.Name,
            Currency = command.Currency,
            Updated = DateTime.UtcNow
        };

        var updateResult = await _groupsRepository.Update(updatedGroup, ct);

        if (updateResult.IsFailure)
        {
            return updateResult.ConvertFailure<Result>();
        }

        return Result.Success();
    }
}