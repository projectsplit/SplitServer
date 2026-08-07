namespace SplitServer.Configuration;

/// <summary>
/// How much to ask for, and how rarely. Every number that decides whether a person sees the
/// donation prompt lives here so the cadence can be loosened or tightened without a deploy of new
/// logic, and so the policy can be read in one place rather than inferred from scattered checks.
/// </summary>
public class DonationsSettings : ISettings
{
    public string SectionName { get; init; } = "Donations";

    /// <summary>
    /// ISO currency everything is charged in. Amounts below are in its minor unit, which assumes a
    /// two-decimal currency — Stripe's zero-decimal currencies (JPY and friends) would need the
    /// conversion factor to become a setting too.
    /// </summary>
    public string Currency { get; set; } = "usd";

    /// <summary>The pre-selected amount. An anchor, not a floor: any amount within the bounds is accepted.</summary>
    public long SuggestedAmountMinor { get; set; } = 1200;

    private static readonly long[] DefaultPresetAmountsMinor = [500, 1200, 2500, 5000];

    /// <summary>
    /// One-tap amounts offered alongside the free-text field. Read through
    /// <see cref="ResolvePresetAmountsMinor"/> rather than directly.
    /// </summary>
    /// <remarks>
    /// Empty by default, and it has to stay that way. Configuration binding does not replace an
    /// array that already holds values — it reads the current one through this property, copies it,
    /// and appends whatever is configured on the end. Giving this an inline default of the four
    /// amounts and also listing those four in appsettings.json produced all eight, and the prompt
    /// drew two rows of buttons. A fallback in the getter does not help either: the binder calls
    /// the getter, so it would append to the fallback. Every scalar setting on this class can carry
    /// an inline default safely; an array cannot.
    /// </remarks>
    public long[] PresetAmountsMinor { get; set; } = [];

    /// <summary>
    /// The amounts to actually offer: whatever is configured, or the built-in defaults if the
    /// setting is absent. A method rather than a property so the config binder never sees it.
    /// </summary>
    public long[] ResolvePresetAmountsMinor() =>
        PresetAmountsMinor.Length > 0
            // Offering the same amount twice is never intended, and two buttons sharing a value
            // collide on their React key in the browser and highlight together.
            ? PresetAmountsMinor.Distinct().ToArray()
            : DefaultPresetAmountsMinor;

    /// <summary>
    /// Stripe rejects charges under roughly $0.50, and fees eat most of anything near that, so
    /// there is no point letting someone through with less.
    /// </summary>
    public long MinAmountMinor { get; set; } = 100;

    /// <summary>A ceiling on a voluntary gift, mostly to catch a misplaced decimal point before the card does.</summary>
    public long MaxAmountMinor { get; set; } = 100_000;

    /// <summary>
    /// How long an account must exist before it is ever asked. Asking someone who has not yet got
    /// anything out of the app reads as a paywall, which is the opposite of what this is.
    /// </summary>
    public int MinAccountAgeDays { get; set; } = 14;

    /// <summary>
    /// Expenses the person must have created before being asked. Account age alone lets in someone
    /// who signed up and never came back; this is the evidence that the app is actually useful to them.
    /// </summary>
    public int MinExpensesCreated { get; set; } = 15;

    /// <summary>
    /// Gap before the second ask. Each later ask doubles it, so the sequence is 90, 180, 360 days —
    /// someone who keeps saying no is asked progressively less rather than on a fixed drumbeat.
    /// </summary>
    public int FirstCooldownDays { get; set; } = 90;

    /// <summary>
    /// Hard ceiling on how many times one person is ever asked, no matter how long they stay. After
    /// this the prompt is done with them for good and the settings entry is the only way in.
    /// </summary>
    public int MaxLifetimePrompts { get; set; } = 4;

    /// <summary>How long someone who has given is left alone. Anyone with a live monthly gift is never asked at all.</summary>
    public int PostDonationCooldownDays { get; set; } = 365;

    /// <summary>Where Stripe returns people, appended to the configured client URL.</summary>
    public string SuccessPath { get; set; } = "/?donation=success";

    public string CancelPath { get; set; } = "/?donation=cancelled";
}
