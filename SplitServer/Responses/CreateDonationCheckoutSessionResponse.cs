namespace SplitServer.Responses;

public class CreateDonationCheckoutSessionResponse
{
    /// <summary>Stripe-hosted Checkout page to send the browser to.</summary>
    public required string CheckoutUrl { get; init; }
}
