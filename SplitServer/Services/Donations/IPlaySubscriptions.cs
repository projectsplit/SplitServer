using CSharpFunctionalExtensions;

namespace SplitServer.Services.Donations;

/// <summary>
/// What following a subscription needs from Google Play: whether Play can be asked at all, what it
/// says about a purchase token, and a way to tell it the purchase has been recorded.
///
/// A seam, and nothing more. <see cref="GooglePlayBillingService"/> is the only real
/// implementation; this exists so the rules for who gets Buqs Plus — which are entirely about what
/// to do with Google's answer — can be exercised without Google on the other end of them.
/// </summary>
public interface IPlaySubscriptions
{
    /// <summary>False on an instance with no usable Play credentials, where every lookup would only fail.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Asks Google what really happened for a subscription purchase. A failure means "could not find
    /// out" or "Play does not know this token", never "the client lied" — the caller must not turn
    /// one into the other.
    /// </summary>
    Task<Result<PlaySubscriptionPurchase>> GetSubscriptionPurchase(string purchaseToken, CancellationToken ct);

    /// <summary>
    /// Tells Play the subscription has been recorded. Play refunds a purchase left unacknowledged
    /// for three days, so this is what makes the money stay.
    /// </summary>
    Task<Result> AcknowledgeSubscription(string productId, string purchaseToken, CancellationToken ct);
}
