namespace SplitServer.Responses;

public class GetDonationPromptResponse
{
    /// <summary>
    /// Whether this person is due to be asked. The client decides <em>when</em> within the session to
    /// show it, but never whether — say no here and no prompt exists.
    /// </summary>
    public required bool ShouldAsk { get; init; }

    /// <summary>
    /// False when the server has no Stripe credentials. The client hides every donation entry point,
    /// including the permanent one in settings, rather than offering a button that cannot work.
    /// </summary>
    public required bool IsAvailable { get; init; }

    public required string Currency { get; init; }

    public required long SuggestedAmountMinor { get; init; }

    public required long[] PresetAmountsMinor { get; init; }

    public required long MinAmountMinor { get; init; }

    public required long MaxAmountMinor { get; init; }

    /// <summary>Lets the permanent settings entry thank an existing supporter instead of asking again.</summary>
    public required bool HasDonated { get; init; }

    public required bool HasActiveMonthly { get; init; }
}
