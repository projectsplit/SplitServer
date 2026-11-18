using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;

namespace SplitServer.Services.Plus;

/// <summary>
/// Which Play product is Buqs Plus, and which of its base plans is which billing period.
///
/// Play will happily verify any subscription bought against this app, including a monthly gift.
/// Looking the product and its plan up here first is what keeps a tip from being read as Plus, and
/// is why a plan this server does not know is refused rather than guessed at.
/// </summary>
public class PlusCatalog
{
    private readonly PlusSettings _settings;

    public PlusCatalog(IOptions<PlusSettings> settings)
    {
        _settings = settings.Value;

        // Annual first: that is the order the plans are offered in, and the first is the one the
        // app starts with selected.
        Plans =
        [
            new PlusPlanOffer(PlusPlan.Annual, _settings.AnnualBasePlanId),
            new PlusPlanOffer(PlusPlan.Monthly, _settings.MonthlyBasePlanId),
        ];
    }

    /// <summary>Whether Plus is switched on. Says nothing about whether Play is reachable.</summary>
    public bool IsEnabled => _settings.Enabled;

    /// <summary>
    /// Whether Plus is live: switched on, and with Play there to verify a purchase. Both, not
    /// either. Switched on with no way to verify would sell something this server could never
    /// grant, and credentials alone are not a decision to start selling. The one definition, so
    /// that selling, the free days and the locks cannot each come to mean something different by it.
    /// </summary>
    public bool IsOffered(bool playIsConfigured) => _settings.Enabled && playIsConfigured;

    public string ProductId => _settings.ProductId;

    /// <summary>How many days before an annual renewal its subscriber is told. Zero or less means never.</summary>
    public int RenewalReminderDays => _settings.RenewalReminderDays;

    /// <summary>How many free days of Plus an account gets. Never negative.</summary>
    public int TrialDays => Math.Max(0, _settings.TrialDays);

    /// <summary>
    /// The instant before which an account has Plus for life, in UTC, or null when that rule is off.
    /// </summary>
    /// <remarks>
    /// Normalised here because of how it arrives. Configuration parses "2026-11-19T00:00:00Z" into
    /// the machine's local time, and a value written without a zone comes through as no zone at
    /// all; compared as-is against an account's creation time, either would move the cut-off by the
    /// server's offset from UTC. A value with no zone is taken to be UTC, which is what it is
    /// documented as.
    /// </remarks>
    public DateTime? LifetimeBefore =>
        _settings.LifetimeBefore switch
        {
            null => null,
            { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            { Kind: DateTimeKind.Unspecified } unzoned => DateTime.SpecifyKind(unzoned, DateTimeKind.Utc),
            var utc => utc,
        };

    /// <summary>How many days before the free days end an account is first told. Zero or less means never.</summary>
    public int TrialReminderDays => _settings.TrialReminderDays;

    /// <summary>How many days before the free days end an account is told for the last time. Zero or less means never.</summary>
    public int TrialFinalReminderDays => _settings.TrialFinalReminderDays;

    /// <summary>Every plan, in the order they should be shown.</summary>
    public IReadOnlyList<PlusPlanOffer> Plans { get; }

    public bool IsPlusProduct(string? productId) =>
        productId is not null && string.Equals(productId, _settings.ProductId, StringComparison.Ordinal);

    /// <summary>
    /// The billing period a base plan stands for, or null for one that is not ours. Exact match
    /// only: a base plan added in the Play Console but not here must not be quietly taken for one
    /// of the two this server knows how to describe.
    /// </summary>
    public PlusPlan? FindPlan(string? basePlanId)
    {
        foreach (var offer in Plans)
        {
            if (string.Equals(offer.BasePlanId, basePlanId, StringComparison.Ordinal))
            {
                return offer.Plan;
            }
        }

        return null;
    }

    /// <summary>
    /// A plan as the API names it. Spelled out rather than left to an enum converter, because the
    /// app matches on these exact strings and a rename of the enum must not reach it.
    /// </summary>
    public static string NameOf(PlusPlan plan) =>
        plan switch
        {
            PlusPlan.Annual => "annual",
            PlusPlan.Monthly => "monthly",
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Not a Plus plan"),
        };
}

/// <summary>One plan on offer: a billing period and the Play base plan that bills it.</summary>
public record PlusPlanOffer(PlusPlan Plan, string BasePlanId);
