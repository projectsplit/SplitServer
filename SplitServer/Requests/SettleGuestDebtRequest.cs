namespace SplitServer.Requests;

public class SettleGuestDebtRequest
{
    public required string GroupId { get; set; }
    public required string GuestId { get; set; }
}