using SplitServer.Models;
using SplitServer.Services.Plus;

namespace SplitServer.Responses;

/// <summary>
/// A person's Plus subscription, as the app is told about it. Sent inside
/// <see cref="PlusAccessResponse"/>, and only for someone whose access is a live subscription at the
/// moment of asking; everybody else gets null in its place.
/// </summary>
public class PlusMembershipResponse
{
    /// <summary>"annual" or "monthly".</summary>
    public required string Plan { get; init; }

    /// <summary>
    /// When the paid period runs out. The renewal date while <see cref="AutoRenewing"/> is true,
    /// and the day Plus ends once it is not.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>False once the subscriber has turned renewal off in Google Play.</summary>
    public required bool AutoRenewing { get; init; }

    /// <summary>
    /// True while a renewal payment has failed and Play is still retrying it. They keep Plus in the
    /// meantime, and fixing the payment method in Google Play is what stops it lapsing.
    /// </summary>
    public required bool InGracePeriod { get; init; }

    /// <summary>What Play will charge at the next renewal, in <see cref="RenewalCurrencyCode"/>. Null when Play does not say.</summary>
    public required decimal? RenewalPrice { get; init; }

    public required string? RenewalCurrencyCode { get; init; }

    /// <summary>
    /// The membership to report for the mark on a user, or null when there is none to report. The
    /// clock is consulted here rather than trusting that the mark is present: a subscription can run
    /// out before anything has come back to take the mark off.
    /// </summary>
    public static PlusMembershipResponse? From(UserPlus? plus, DateTime now)
    {
        if (plus is null || !PlusEntitlement.IsEntitled(plus, now))
        {
            return null;
        }

        return new PlusMembershipResponse
        {
            Plan = PlusCatalog.NameOf(plus.Plan),
            ExpiresAt = plus.ExpiresAt,
            AutoRenewing = plus.AutoRenewing,
            InGracePeriod = PlusEntitlement.IsInGracePeriod(plus.State),
            RenewalPrice = plus.RenewalPrice,
            RenewalCurrencyCode = plus.RenewalCurrency,
        };
    }
}
