namespace SplitServer.Requests;

public class CreateDonationCheckoutSessionRequest
{
    /// <summary>Amount in the donation currency's minor unit. Bounds-checked server-side; never trusted as sent.</summary>
    public required long AmountMinor { get; init; }

    public required bool Monthly { get; init; }
}
