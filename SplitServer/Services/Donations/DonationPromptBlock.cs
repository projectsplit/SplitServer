namespace SplitServer.Services.Donations;

/// <summary>Why someone is not being asked, or <see cref="None"/> if they are.</summary>
public enum DonationPromptBlock
{
    None = 0,
    NotConfigured,
    OptedOut,
    HasActiveMonthly,
    RecentlyDonated,
    AccountTooNew,
    LifetimeLimitReached,
    WithinCooldown,
    NotEngagedEnough,
}
