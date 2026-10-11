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

    /// <summary>How to build the product for a typed "Other amount". Null when that option is off.</summary>
    public required CustomAmountResponse? CustomAmount { get; init; }

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

/// <summary>
/// The "Other amount" family: one product per amount from <see cref="StepMinor"/> to
/// <see cref="MaxMinor"/> in steps of <see cref="StepMinor"/>, named by appending the amount in minor
/// units to a prefix (1750 for 17.50). The client builds the id for whatever was typed and asks Play
/// for that one product, rather than being sent hundreds of ids up front.
/// </summary>
public class CustomAmountResponse
{
    public required long MaxMinor { get; init; }

    public required int StepMinor { get; init; }

    public required string OneTimeProductPrefix { get; init; }

    public required string MonthlyProductPrefix { get; init; }

    public required string MonthlyBasePlanId { get; init; }
}
