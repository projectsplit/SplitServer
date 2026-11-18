namespace SplitServer.Configuration;

/// <summary>
/// Buqs Plus, the paid tier. One subscription product in the Play Console with a base plan per
/// billing period; Play owns the prices, so nothing here says what either plan costs.
///
/// The shape of the deal is here too: everybody gets Plus free for a while, the accounts that were
/// there before it cost anything keep it for good, and after that it is a subscription. The core of
/// Buqs is free throughout and none of this touches it.
/// </summary>
public class PlusSettings : ISettings
{
    public string SectionName { get; init; } = "Plus";

    /// <summary>
    /// Whether Plus is live. On, Plus is for sale and the free days start counting; off, nobody is
    /// shown a plan or a price and every account has everything, exactly as before Plus existed.
    /// </summary>
    /// <remarks>
    /// It is a switch on selling and on locking, never on honouring. Someone who already subscribed
    /// keeps what they paid for, their renewals are still followed, and a purchase Play has already
    /// taken is still recorded, whichever way this is set. It also needs Google Play credentials to
    /// mean anything: switched on with no way to verify a purchase, Plus counts as not live.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>The subscription's product id. Must exist in the Play Console under exactly this id.</summary>
    public string ProductId { get; set; } = "buqs_plus";

    /// <summary>Base plan billed every month. Must match the base plan id in the Play Console.</summary>
    public string MonthlyBasePlanId { get; set; } = "monthly";

    /// <summary>Base plan billed once a year. Must match the base plan id in the Play Console.</summary>
    public string AnnualBasePlanId { get; set; } = "annual";

    /// <summary>
    /// How long before an annual plan renews its subscriber is told. A year is long enough to forget
    /// a subscription exists, and a charge nobody saw coming is the one that gets disputed. Monthly
    /// plans get no reminder: a notice every four weeks is noise, not a warning. Zero turns it off.
    /// </summary>
    public int RenewalReminderDays { get; set; } = 7;

    /// <summary>
    /// How many days of Plus every account gets for nothing, with no card and nothing to cancel.
    /// Counted from when the account was made, or from the moment Plus went live if that is later,
    /// so nobody's free days are used up before there was anything to use them on.
    /// </summary>
    public int TrialDays { get; set; } = 30;

    /// <summary>
    /// Accounts created before this instant have Plus for life: the people who were using Buqs
    /// before Plus cost anything. UTC. Null turns the rule off, leaving only accounts that carry
    /// the grant on their own document.
    /// </summary>
    /// <remarks>
    /// A fixed moment rather than "whoever exists at go-live", on purpose. It is the promise made to
    /// a known set of people, and it must not quietly grow to take in everyone who signs up between
    /// that promise and the day the switch is thrown.
    ///
    /// To switch the rule off from configuration, set a date earlier than any account, such as
    /// 2000-01-01. An empty value does not do it: configuration binding leaves a default alone when
    /// it is given nothing to put in its place, so blanking the setting keeps the date below.
    /// </remarks>
    public DateTime? LifetimeBefore { get; set; } = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// How long before the free days run out an account is first told. Zero turns this notice off.
    /// </summary>
    public int TrialReminderDays { get; set; } = 7;

    /// <summary>
    /// How long before the free days run out an account is told the second and last time. Zero
    /// turns this notice off.
    /// </summary>
    public int TrialFinalReminderDays { get; set; } = 1;
}
