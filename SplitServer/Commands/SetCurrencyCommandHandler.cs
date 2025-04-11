using CSharpFunctionalExtensions;
using MediatR;
using NMoneys;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SetCurrencyCommandHandler : IRequestHandler<SetCurrencyCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public SetCurrencyCommandHandler(
        IUsersRepository usersRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result> Handle(SetCurrencyCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(command.UserId, ct);

        if (!Currency.TryGet(command.Currency, out _))
        {
            return Result.Failure("Currency should be a valid ISO 4217 code");
        }

        var now = DateTime.Now;

        var userPreferences = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value with
            {
                Currency = command.Currency,
                Updated = now
            }
            : new UserPreferences
            {
                Id = command.UserId,
                Created = now,
                Updated = now,
                TimeZone = null,
                Currency = command.Currency,
            };

        return await _userPreferencesRepository.Upsert(userPreferences, ct);
    }
}