namespace SplitServer.Requests;

public class RegisterPlusPurchaseRequest
{
    /// <summary>
    /// Play's receipt for the subscription. Deliberately the only field: which product and plan it
    /// is for, who bought it and whether it is paid are all Google's to say, not the app's.
    /// </summary>
    public required string PurchaseToken { get; init; }
}
