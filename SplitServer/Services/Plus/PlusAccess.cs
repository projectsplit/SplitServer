using SplitServer.Models;

namespace SplitServer.Services.Plus;

/// <summary>Why an account has the Plus features, or that it does not.</summary>
public enum PlusAccessKind
{
    /// <summary>Plus is not live here. Nothing is sold and nothing is locked: everybody has everything.</summary>
    Open = 0,

    /// <summary>Paying for it.</summary>
    Subscription,

    /// <summary>Has it for good, as one of the accounts from before Plus cost anything or by a grant.</summary>
    Lifetime,

    /// <summary>Inside the free days every account starts with.</summary>
    Trial,

    /// <summary>The free days are over and nothing else applies. The Plus features need a subscription.</summary>
    None,
}

/// <summary>What one account has, at one moment, and the facts the app needs to say so.</summary>
public record PlusAccess
{
    public required PlusAccessKind Kind { get; init; }

    /// <summary>
    /// When the free days end, or ended. Set for <see cref="PlusAccessKind.Trial"/> and
    /// <see cref="PlusAccessKind.None"/>, where it is the thing to tell the person, and null for the
    /// others, for whom it decides nothing.
    /// </summary>
    public required DateTime? TrialEndsAt { get; init; }

    /// <summary>The subscription giving access. Set only for <see cref="PlusAccessKind.Subscription"/>.</summary>
    public required UserPlus? Subscription { get; init; }

    /// <summary>
    /// Whether the Plus features are available to this account right now. Everything but
    /// <see cref="PlusAccessKind.None"/>: not live means nothing is locked.
    /// </summary>
    public bool HasPlus => Kind != PlusAccessKind.None;
}

/// <summary>
/// The server-wide half of the question: what is true for every account at this moment. Passed in
/// rather than looked up by the rule, so the rule itself stays a pure function of its arguments.
/// </summary>
/// <param name="IsOffered">Whether Plus is live: switched on, with Play there to verify a purchase.</param>
/// <param name="LiveSince">When Plus went live. Free days cannot start before this.</param>
/// <param name="TrialDays">How many free days an account gets.</param>
/// <param name="LifetimeBefore">Accounts created before this have Plus for life. Null turns the rule off.</param>
public record PlusTerms(bool IsOffered, DateTime LiveSince, int TrialDays, DateTime? LifetimeBefore);

/// <summary>
/// Decides what access an account has to the Plus features. One function, used by everything that
/// asks, so that the app, the notices and the donation prompt cannot each reach a different answer
/// about the same person.
///
/// It only says what an account has. Nothing on the server refuses a request because of it: the
/// app is what shows or locks a feature.
/// </summary>
public static class PlusAccessRule
{
    /// <summary>
    /// The access <paramref name="user"/> has at <paramref name="now"/>. The order of the checks is
    /// the rule:
    /// <list type="number">
    /// <item>Plus is not live: everything is open, to everyone, whatever else is true of them.</item>
    /// <item>A live subscription. First among the real answers, so that someone who has Plus for
    /// life and is somehow also paying can still see the subscription and cancel it.</item>
    /// <item>Plus for life: the grant on the account, or simply being older than the cut-off. The
    /// second is checked as well as the first so that an account the boot-time grant has not reached
    /// yet is not treated as a newcomer in the meantime.</item>
    /// <item>The free days, counted from the account's creation or from Plus going live, whichever
    /// is later.</item>
    /// <item>Otherwise nothing.</item>
    /// </list>
    /// </summary>
    public static PlusAccess Evaluate(User user, PlusTerms terms, DateTime now)
    {
        if (!terms.IsOffered)
        {
            return Without(PlusAccessKind.Open);
        }

        if (PlusEntitlement.IsEntitled(user.Plus, now))
        {
            return new PlusAccess { Kind = PlusAccessKind.Subscription, TrialEndsAt = null, Subscription = user.Plus };
        }

        if (user.PlusForLife is not null ||
            (terms.LifetimeBefore is { } cutOff && user.Created < cutOff))
        {
            return Without(PlusAccessKind.Lifetime);
        }

        var trialEndsAt = TrialEndsAt(user.Created, terms.LiveSince, terms.TrialDays);

        return new PlusAccess
        {
            Kind = now < trialEndsAt ? PlusAccessKind.Trial : PlusAccessKind.None,
            TrialEndsAt = trialEndsAt,
            Subscription = null,
        };
    }

    /// <summary>
    /// When an account's free days end. They start when the account was made, or when Plus went
    /// live if that came later — an account older than Plus gets its full allowance from the day
    /// there was something to spend it on, not a remainder of days that passed before the rule
    /// existed.
    /// </summary>
    public static DateTime TrialEndsAt(DateTime accountCreated, DateTime liveSince, int trialDays)
    {
        var startsAt = accountCreated > liveSince ? accountCreated : liveSince;

        return startsAt.AddDays(Math.Max(0, trialDays));
    }

    /// <summary>
    /// A kind of access as the API names it. Spelled out rather than left to an enum converter,
    /// because the app matches on these exact strings and a rename of the enum must not reach it.
    /// </summary>
    public static string NameOf(PlusAccessKind kind) =>
        kind switch
        {
            PlusAccessKind.Open => "open",
            PlusAccessKind.Subscription => "subscription",
            PlusAccessKind.Lifetime => "lifetime",
            PlusAccessKind.Trial => "trial",
            PlusAccessKind.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of Plus access"),
        };

    private static PlusAccess Without(PlusAccessKind kind) =>
        new() { Kind = kind, TrialEndsAt = null, Subscription = null };
}
