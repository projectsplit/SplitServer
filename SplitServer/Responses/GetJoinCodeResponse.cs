namespace SplitServer.Responses;

public class GetJoinCodeResponse
{
    public required bool IsAlreadyMember { get; init; }
    public required string GroupId { get; init; }
    public required string GroupName { get; init; }
    public required bool IsExpired  { get; init; }
}