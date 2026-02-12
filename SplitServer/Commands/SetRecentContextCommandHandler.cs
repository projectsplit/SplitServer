using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SetRecentContextCommandHandler : IRequestHandler<SetRecentContextCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public SetRecentContextCommandHandler(
        IUsersRepository usersRepository,
        IUserActivityRepository userActivityRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _userActivityRepository = userActivityRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(SetRecentContextCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }
        
        if (command.ContextId != "NON_GROUP")
        {
            var groupMaybe = await _groupsRepository.GetById(command.ContextId, ct);

            if (groupMaybe.HasNoValue)
            {
                return Result.Failure($"Group with id {command.ContextId} was not found");
            }

            var group = groupMaybe.Value;

            if (group.Members.All(x => x.UserId != command.UserId))
            {
                return Result.Failure<Result>("User is not a group member");
            }
        }

        var userActivityMaybe = await _userActivityRepository.GetById(command.UserId, ct);

        var now = DateTime.Now;

        var userActivity = userActivityMaybe.HasValue
            ? userActivityMaybe.Value with
            {
                RecentContextId = command.ContextId,
                Updated = now
            }
            : new UserActivity
            {
                Id = command.UserId,
                Created = now,
                Updated = now,
                RecentContextId = command.ContextId,
                LastViewedNotificationTimestamp = null
            };

        return await _userActivityRepository.Upsert(userActivity, ct);
    }
}