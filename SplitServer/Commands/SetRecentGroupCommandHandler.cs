using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SetRecentGroupCommandHandler : IRequestHandler<SetRecentGroupCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public SetRecentGroupCommandHandler(
        IUsersRepository usersRepository,
        IUserActivityRepository userActivityRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _userActivityRepository = userActivityRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result> Handle(SetRecentGroupCommand command, CancellationToken ct)
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
            return Result.Failure<Result>("User is not a group member");
        }

        var userActivityMaybe = await _userActivityRepository.GetById(command.UserId, ct);

        var now = DateTime.Now;

        var userActivity = userActivityMaybe.HasValue
            ? userActivityMaybe.Value with
            {
                RecentGroupId = command.GroupId,
                Updated = now
            }
            : new UserActivity
            {
                Id = command.UserId,
                IsDeleted = false,
                Created = now,
                Updated = now,
                RecentGroupId = command.GroupId,
                LastViewedNotificationTimestamp = null
            };

        return await _userActivityRepository.Upsert(userActivity, ct);
    }
}