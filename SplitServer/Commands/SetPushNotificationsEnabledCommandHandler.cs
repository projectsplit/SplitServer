using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SetPushNotificationsEnabledCommandHandler : IRequestHandler<SetPushNotificationsEnabledCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public SetPushNotificationsEnabledCommandHandler(
        IUsersRepository usersRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result> Handle(SetPushNotificationsEnabledCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(command.UserId, ct);

        var now = DateTime.UtcNow;

        var userPreferences = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value with
            {
                PushNotificationsEnabled = command.Enabled,
                Updated = now
            }
            : new UserPreferences
            {
                Id = command.UserId,
                Created = now,
                Updated = now,
                Currency = null,
                TimeZone = null,
                PushNotificationsEnabled = command.Enabled,
            };

        return await _userPreferencesRepository.Upsert(userPreferences, ct);
    }
}
