namespace SplitServer.Requests;

public class RevokeInvitationRequest
{
    public required string GroupId { get; init; }
    public required string ReceiverId { get; init; }
}