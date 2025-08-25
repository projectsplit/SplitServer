namespace SplitServer.Requests;

public class SendInvitationRequest
{
    public required string ReceiverId { get; set; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }
}