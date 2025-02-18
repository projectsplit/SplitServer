using SplitServer.Models;

namespace SplitServer.Dto;

public class GetUserInvitationsResponse
{
    public required List<Invitation> Invitations { get; init; }
    public required string? Next { get; init; }
}