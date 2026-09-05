namespace SplitServer.Responses;

public class GetDonationPromptResponse
{
    /// <summary>
    /// Whether this person is due to be asked. The client decides <em>when</em> within the session to
    /// show it, but never whether — say no here and no prompt exists.
    /// </summary>
    public required bool ShouldAsk { get; init; }

    /// <summary>
    /// False when the server has no Google Play credentials. The client hides every donation entry
    /// point, including the permanent one in settings, rather than offering a button that cannot work.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// The tiers on offer, in the order they should be shown. Carries no prices: Play sets those per
    /// country and the device reads them from Play itself, so the only price anyone is ever shown is
    /// the one they will actually be charged.
    /// </summary>
    public required DonationProductResponse[] Products { get; init; }

    /// <summary>Lets the permanent settings entry thank an existing supporter instead of asking again.</summary>
    public required bool HasDonated { get; init; }

    public required bool HasActiveMonthly { get; init; }
}

public class DonationProductResponse
{
    /// <summary>Play in-app product id. The client asks Play about this id to get a price to show.</summary>
    public required string ProductId { get; init; }

    /// <summary>0 for a one-off, 1 for monthly. Mirrors the server's DonationKind.</summary>
    public required int Kind { get; init; }

    /// <summary>Base plan id, needed to buy a subscription and null for a one-off.</summary>
    public required string? BasePlanId { get; init; }
}
