namespace SplitServer.Requests;

public class RegisterDonationPurchaseRequest
{
    /// <summary>Must be one of the configured donation products; anything else is refused.</summary>
    public required string ProductId { get; init; }

    public required string PurchaseToken { get; init; }
}
