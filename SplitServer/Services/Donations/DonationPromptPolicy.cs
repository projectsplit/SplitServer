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
    /// <param name="isPlusMember">
    /// Whether this person has Buqs Plus right now. Passed in rather than read off the state
    /// because it lives on the user, which the caller already has in hand.
    /// </param>
    public DonationPromptBlock EvaluateWithoutEngagement(
        DonationPromptState state,
        DateTime accountCreated,
        DateTime now,
        bool isPlusMember = false)
    {
        if (state.OptedOut)
        {
            return DonationPromptBlock.OptedOut;
        }

        // Someone paying for Plus is already paying for Buqs. Asking them for a tip on top is the
        // one ask here that could only ever read as ungrateful, so it is never made — for as long
        // as they have Plus, however long it has been since anything else was asked of them.
        if (isPlusMember)
        {
            return DonationPromptBlock.PlusMember;
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
}
