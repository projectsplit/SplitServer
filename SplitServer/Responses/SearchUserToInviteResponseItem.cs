namespace SplitServer.Responses;

public class SearchUserToInviteResponseItem
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required bool IsGroupMember { get; init; }
    public required bool IsAlreadyInvited { get; init; }
}