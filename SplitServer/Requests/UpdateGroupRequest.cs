namespace SplitServer.Requests;

public class UpdateGroupRequest
{
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
}