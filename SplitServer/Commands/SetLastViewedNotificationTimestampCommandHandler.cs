using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SetLastViewedNotificationTimestampCommandHandler : IRequestHandler<SetLastViewedNotificationTimestampCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public SetLastViewedNotificationTimestampCommandHandler(
        IUsersRepository usersRepository,
        IUserActivityRepository userActivityRepository)
    {
        _usersRepository = usersRepository;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<Result> Handle(SetLastViewedNotificationTimestampCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var userActivityMaybe = await _userActivityRepository.GetById(command.UserId, ct);

        var now = DateTime.Now;

        var userActivity = userActivityMaybe.HasValue
            ? userActivityMaybe.Value with
            {
                LastViewedNotificationTimestamp = command.Timestamp,
                Updated = now
            }
            : new UserActivity
            {
                Id = command.UserId,
                Created = now,
                Updated = now,
                RecentGroupId = null,
                LastViewedNotificationTimestamp = command.Timestamp
            };

        return await _userActivityRepository.Upsert(userActivity, ct);
    }
}