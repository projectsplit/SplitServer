using SplitServer.Services.Plus;

namespace SplitServer.Responses;

/// <summary>
/// What access an account has to the Plus features, as the app is told. Always present, for every
/// account: the app decides what to show or lock from this alone, so there is no case in which it
/// is left to guess from a missing field.
/// </summary>
public class PlusAccessResponse
{
    /// <summary>
    /// One of "open", "subscription", "lifetime", "trial" or "none".
    /// <list type="bullet">
    /// <item>"open": Plus is not live. Nothing is sold and nothing is locked.</item>
    /// <item>"subscription": paying for it; <see cref="Subscription"/> says how.</item>
    /// <item>"lifetime": has it for good.</item>
    /// <item>"trial": inside the free days, which end at <see cref="TrialEndsAt"/>.</item>
    /// <item>"none": the free days ended at <see cref="TrialEndsAt"/>; the Plus features need a subscription.</item>
    /// </list>
    /// </summary>
    public required string Access { get; init; }

    /// <summary>When the free days end or ended. Set for "trial" and "none", null for the rest.</summary>
    public required DateTime? TrialEndsAt { get; init; }

    /// <summary>The subscription giving access. Set only when <see cref="Access"/> is "subscription".</summary>
    public required PlusMembershipResponse? Subscription { get; init; }

    public static PlusAccessResponse From(PlusAccess access, DateTime now) =>
        new()
        {
            Access = PlusAccessRule.NameOf(access.Kind),
            TrialEndsAt = access.TrialEndsAt,
            Subscription = access.Kind == PlusAccessKind.Subscription
                ? PlusMembershipResponse.From(access.Subscription, now)
                : null,
        };
}
