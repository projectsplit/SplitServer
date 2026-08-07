using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;

namespace SplitServer.Services.Donations;

/// <summary>
/// Decides whether a person should be asked to contribute. All of it is here, none of it in the
/// browser: the client can only ever be trusted to pick a good moment, not to enforce a limit, and
/// state kept in a browser resets when someone clears it or opens the app on their phone.
///
/// The shape of the rules matters more than any single number. Nobody is asked before the app has
/// been useful to them, the gap between asks doubles each time, and there is a hard ceiling after
/// which the prompt never appears again. That makes the worst case a handful of asks over roughly
/// two years, rather than something that keeps returning as long as the person keeps using the app.
/// </summary>
public class DonationPromptPolicy
{
    private readonly DonationsSettings _settings;

    public DonationPromptPolicy(IOptions<DonationsSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Every gate except the one that costs a query. Kept separate so the caller can run the cheap
    /// checks first and only reach for the expense count on the rare pass that gets that far.
    /// </summary>
    public DonationPromptBlock EvaluateWithoutEngagement(
        DonationPromptState state,
        DateTime accountCreated,
        DateTime now)
    {
        if (state.OptedOut)
        {
            return DonationPromptBlock.OptedOut;
        }

        // Someone already giving every month is the last person who should be asked for more.
        if (state.HasActiveMonthly)
        {
            return DonationPromptBlock.HasActiveMonthly;
        }

        if (state.LastDonatedAt is { } lastDonated &&
            now < lastDonated.AddDays(_settings.PostDonationCooldownDays))
        {
            return DonationPromptBlock.RecentlyDonated;
        }

        if (now < accountCreated.AddDays(_settings.MinAccountAgeDays))
        {
            return DonationPromptBlock.AccountTooNew;
        }

        if (state.PromptCount >= _settings.MaxLifetimePrompts)
        {
            return DonationPromptBlock.LifetimeLimitReached;
        }

        if (state.LastPromptedAt is { } lastPrompted &&
            now < lastPrompted.AddDays(CooldownDaysAfter(state.PromptCount)))
        {
            return DonationPromptBlock.WithinCooldown;
        }

        return DonationPromptBlock.None;
    }

    /// <summary>
    /// Whether the expense count still needs running. Once the bar has been cleared the answer is
    /// recorded on the state and never revisited — having got value out of the app is not something
    /// that becomes untrue later, and deleting old expenses should not put someone back below it.
    /// </summary>
    public bool NeedsEngagementCheck(DonationPromptState state) => state.EngagementReachedAt is null;

    public int MinExpensesCreated => _settings.MinExpensesCreated;

    /// <summary>
    /// Days to wait after the ask numbered <paramref name="promptCount"/>. Doubles each time, so a
    /// person who keeps declining is asked at 90 days, then 180, then 360.
    /// </summary>
    public int CooldownDaysAfter(int promptCount)
    {
        // Doubling is unbounded on paper; the ceiling only exists so a corrupt count cannot overflow.
        var doublings = Math.Clamp(promptCount - 1, 0, 10);

        return _settings.FirstCooldownDays * (1 << doublings);
    }

    public bool IsAmountAllowed(long amountMinor) =>
        amountMinor >= _settings.MinAmountMinor && amountMinor <= _settings.MaxAmountMinor;

    public string AmountOutOfRangeMessage() =>
        $"Amount must be between {_settings.MinAmountMinor} and {_settings.MaxAmountMinor} " +
        $"{_settings.Currency.ToUpperInvariant()} minor units";
}
