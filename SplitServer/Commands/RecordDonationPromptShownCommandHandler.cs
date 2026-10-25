using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

/// <summary>
/// Starts the cooldown. Reported by the client at the moment the prompt actually reaches the screen
/// rather than inferred from the eligibility call, because that call happens on app load and the
/// client may well decide never to show anything — counting it as an ask would silently burn one of
/// the four a person ever gets.
/// </summary>
public class RecordDonationPromptShownCommandHandler : IRequestHandler<RecordDonationPromptShownCommand, Result>
{
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;

    public RecordDonationPromptShownCommandHandler(IDonationPromptStatesRepository donationPromptStatesRepository)
    {
        _donationPromptStatesRepository = donationPromptStatesRepository;
    }

    public async Task<Result> Handle(RecordDonationPromptShownCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var stateMaybe = await _donationPromptStatesRepository.GetById(command.UserId, ct);

        var state = stateMaybe.HasValue
            ? stateMaybe.Value
            : DonationPromptState.CreateEmpty(command.UserId, now);

        var updated = state with
        {
            LastPromptedAt = now,
            PromptCount = state.PromptCount + 1,
            Updated = now,
        };

        return await _donationPromptStatesRepository.Upsert(updated, ct);
    }
}
