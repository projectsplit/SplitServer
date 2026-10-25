using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DismissDonationPromptCommandHandler : IRequestHandler<DismissDonationPromptCommand, Result>
{
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;

    public DismissDonationPromptCommandHandler(IDonationPromptStatesRepository donationPromptStatesRepository)
    {
        _donationPromptStatesRepository = donationPromptStatesRepository;
    }

    public async Task<Result> Handle(DismissDonationPromptCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var stateMaybe = await _donationPromptStatesRepository.GetById(command.UserId, ct);

        var state = stateMaybe.HasValue
            ? stateMaybe.Value
            : DonationPromptState.CreateEmpty(command.UserId, now);

        // A plain "not now" changes nothing here: the ask was already counted when it was shown, and
        // that is what the cooldown runs from. Recording the dismissal separately would double-count
        // it against the lifetime limit.
        var updated = state with
        {
            OptedOut = state.OptedOut || command.OptOut,
            Updated = now,
        };

        return await _donationPromptStatesRepository.Upsert(updated, ct);
    }
}
