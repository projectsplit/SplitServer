namespace SplitServer.Responses;

public class GetUserInvitationsResponse
{
    public required List<InvitationResponseItem> Invitations { get; init; }
    public required string? Next { get; init; }
}