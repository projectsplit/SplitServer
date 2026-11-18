using SplitServer.Models;

namespace SplitServer.Services.Plus;

/// <summary>
/// The rules for who has Buqs Plus, and for when a subscription wants looking at again. Every one
/// of them is a pure question about a row and a clock, kept here so that the recorder, the worker
/// and the API cannot each grow their own slightly different answer.
/// </summary>
public static class PlusEntitlement
{
    private const string Active = "SUBSCRIPTION_STATE_ACTIVE";
    private const string InGracePeriod = "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";
    private const string Canceled = "SUBSCRIPTION_STATE_CANCELED";
    private const string Pending = "SUBSCRIPTION_STATE_PENDING";
    private const string Expired = "SUBSCRIPTION_STATE_EXPIRED";
    private const string PendingPurchaseCanceled = "SUBSCRIPTION_STATE_PENDING_PURCHASE_CANCELED";

    /// <summary>
    /// Ours, not Play's: written on a token that a newer purchase has taken over from, so the old
    /// row stops being asked about. Play itself would go on calling it whatever it last was.
    /// </summary>
    public const string Replaced = "SUBSCRIPTION_STATE_REPLACED";

    /// <summary>
    /// States in which the person has what they paid for, as long as the period has not run out.
    /// Cancelled is deliberately among them: cancelling stops the next payment, not the time already
    /// bought. On hold, paused and pending are deliberately not — no money is covering today.
    /// </summary>
    private static readonly HashSet<string> EntitledStates = [Active, InGracePeriod, Canceled];

    /// <summary>
    /// States nothing ever leaves. Coming back after one of these is a new purchase with a new
    /// token, so there is no reason to ask Play about the old one again.
    /// </summary>
    public static readonly IReadOnlyCollection<string> EndedStates = [Expired, PendingPurchaseCanceled, Replaced];

    /// <summary>
    /// How long a subscription that is due to renew goes on counting after its period has run out,
    /// while this server finds out what happened.
    /// </summary>
    /// <remarks>
    /// The date on a row is the end of the period that was paid for when the row was last read. At
    /// that instant Play renews the subscription, and the new date only reaches this server when it
    /// is told or goes and asks — which, with the worker as the fallback, can be an hour later.
    /// Without this, every subscriber would lose Plus for that hour on every single renewal, having
    /// done nothing but pay on time. The allowance covers a few passes of the worker and no more:
    /// if the payment in fact failed, the re-read says so and Plus stops there. It is never given
    /// to someone who turned renewal off, because for them the date really is the end.
    /// </remarks>
    public static readonly TimeSpan RenewalLeeway = TimeSpan.FromHours(3);

    /// <summary>
    /// How stale a live row may get before it is re-read whatever its dates say. A refund or a
    /// revocation changes nothing the row could predict, and when Play's notifications are not
    /// connected this is the only way one is ever noticed.
    /// </summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether a subscription in <paramref name="state"/> that runs to <paramref name="expiresAt"/>
    /// gives Plus at <paramref name="now"/>. Both halves are needed: the state alone is only as
    /// fresh as the last time Play was asked, and a date alone would keep paying out on a
    /// subscription that was refunded.
    /// </summary>
    public static bool IsEntitled(string state, DateTime? expiresAt, bool autoRenewing, DateTime now)
    {
        if (!EntitledStates.Contains(state) || expiresAt is not { } end)
        {
            return false;
        }

        if (end > now)
        {
            return true;
        }

        // Past its date, but active and set to renew: most likely renewed a moment ago and not yet
        // re-read. See RenewalLeeway.
        return state == Active && autoRenewing && end > now - RenewalLeeway;
    }

    public static bool IsEntitled(PlusSubscription subscription, DateTime now) =>
        IsEntitled(subscription.State, subscription.ExpiresAt, subscription.AutoRenewing, now);

    /// <summary>Whether the mark on a user still counts. A missing mark never does.</summary>
    public static bool IsEntitled(UserPlus? plus, DateTime now) =>
        plus is not null && IsEntitled(plus.State, plus.ExpiresAt, plus.AutoRenewing, now);

    /// <summary>
    /// Whether Play has actually been paid for a subscription in this state, whatever the dates
    /// say. This is the test for telling Play to keep the money: a purchase whose payment is still
    /// pending must not be acknowledged, and Play refuses the attempt.
    /// </summary>
    public static bool IsPaidFor(string state) => EntitledStates.Contains(state);

    /// <summary>
    /// True while a renewal payment has failed and Play is still retrying it. The person keeps Plus
    /// meanwhile, and it is worth telling them, because the fix — a card that works — is theirs.
    /// </summary>
    public static bool IsInGracePeriod(string state) => state == InGracePeriod;

    public static bool HasEnded(string state) => EndedStates.Contains(state);

    /// <summary>
    /// The subscription that gives this person Plus right now, out of all the ones they have ever
    /// had, or null if none does. The one that lasts longest wins, which only matters in the
    /// overlap where a new purchase and the one it replaces are both still inside their period.
    /// </summary>
    public static PlusSubscription? Current(IEnumerable<PlusSubscription> subscriptions, DateTime now) =>
        subscriptions
            .Where(x => IsEntitled(x, now))
            .MaxBy(x => x.ExpiresAt);

    /// <summary>The mark to put on the user for <paramref name="current"/>, or null to take it off.</summary>
    public static UserPlus? ToUserPlus(PlusSubscription? current)
    {
        // Entitlement requires an expiry, so this is only ever null for a row that is not current.
        if (current?.ExpiresAt is not { } expiresAt)
        {
            return null;
        }

        return new UserPlus
        {
            Plan = current.Plan,
            ExpiresAt = expiresAt,
            AutoRenewing = current.AutoRenewing,
            State = current.State,
            SubscriptionId = current.Id,
            RenewalPrice = current.RenewalPrice,
            RenewalCurrency = current.RenewalCurrency,
            Since = current.StartedAt ?? current.Created,
        };
    }

    /// <summary>
    /// Whether Play should be asked about this subscription again without waiting to be told.
    ///
    /// Three reasons. Its period has run out, in which case it has either renewed or lapsed and
    /// the row says neither. Its first payment is still on its way, by some slow payment method,
    /// and the person gets nothing until it lands, so it is looked for on every pass rather than
    /// once a day. Or it simply has not been looked at for a day. A finished subscription is never
    /// due: nothing about it can change.
    /// </summary>
    public static bool IsRefreshDue(PlusSubscription subscription, DateTime now)
    {
        if (HasEnded(subscription.State))
        {
            return false;
        }

        var periodIsOver = subscription.ExpiresAt is { } end && end <= now;
        var paymentIsOnItsWay = subscription.State == Pending;

        return periodIsOver || paymentIsOnItsWay || subscription.Updated <= now - MaxAge;
    }

    /// <summary>
    /// Whether the person should be told now that their plan is about to renew.
    ///
    /// Annual plans only, and only one that really will renew: active, with renewal still on. A
    /// cancelled plan has nothing coming, and one in its grace period is past its renewal date
    /// already — "renews soon" would be wrong for both. Once per renewal, which is what comparing
    /// against the date the last reminder was for achieves.
    /// </summary>
    public static bool IsReminderDue(PlusSubscription subscription, DateTime now, int reminderDays)
    {
        if (reminderDays <= 0 ||
            subscription.Plan != PlusPlan.Annual ||
            !subscription.AutoRenewing ||
            subscription.State != Active ||
            subscription.ExpiresAt is not { } renewsAt)
        {
            return false;
        }

        return renewsAt > now &&
               renewsAt <= now.AddDays(reminderDays) &&
               subscription.RenewalReminderSentFor != renewsAt;
    }
}
