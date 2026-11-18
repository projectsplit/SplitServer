using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Commands;

/// <summary>
/// The app reporting a Plus subscription it has just bought. Nothing here trusts that report beyond
/// the one string it carries: the token is taken to Google, and what Google says about it — which
/// product, which plan, whose, paid or not — is what decides whether anyone gets Plus.
///
/// Deliberately not gated on Plus being switched on for sale. That switch decides whether a price
/// is shown; by the time a token reaches here Play has already taken the money, and refusing to
/// record it would leave someone charged for a subscription they were never given.
/// </summary>
public class RegisterPlusPurchaseCommandHandler : IRequestHandler<RegisterPlusPurchaseCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPlaySubscriptions _play;
    private readonly PlusRecorder _recorder;

    public RegisterPlusPurchaseCommandHandler(
        IUsersRepository usersRepository,
        IPlaySubscriptions play,
        PlusRecorder recorder)
    {
        _usersRepository = usersRepository;
        _play = play;
        _recorder = recorder;
    }

    public async Task<Result> Handle(RegisterPlusPurchaseCommand command, CancellationToken ct)
    {
        if (!_play.IsConfigured)
        {
            return Result.Failure("Buqs Plus is not available");
        }

        if (string.IsNullOrWhiteSpace(command.PurchaseToken))
        {
            return Result.Failure("A purchase token is required");
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        return await _recorder.Record(command.PurchaseToken, command.UserId, ct);
    }
}
