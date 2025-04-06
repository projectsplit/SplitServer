namespace SplitServer.Requests;

public class EditGroupRequest
{
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
}