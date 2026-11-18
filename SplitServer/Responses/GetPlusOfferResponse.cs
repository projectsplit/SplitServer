namespace SplitServer.Responses;

public class GetPlusOfferResponse
{
    /// <summary>
    /// False when Plus is not switched on for sale, or when the server has no Google Play
    /// credentials to verify a purchase with. The client then shows no plans and no prices.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>The Play subscription product. The client asks Play about this id to get prices to show.</summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// The plans on offer, in the order they should be shown; the first is the one to start with
    /// selected. Carries no prices: Play sets those per country and the device reads them from Play
    /// itself, so the only price anyone is ever shown is the one they will actually be charged.
    /// Empty when <see cref="IsAvailable"/> is false.
    /// </summary>
    public required PlusPlanResponse[] Plans { get; init; }
}

public class PlusPlanResponse
{
    /// <summary>"annual" or "monthly".</summary>
    public required string Plan { get; init; }

    /// <summary>The Play base plan to buy for this billing period.</summary>
    public required string BasePlanId { get; init; }
}
