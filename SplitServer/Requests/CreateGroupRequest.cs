namespace SplitServer.Requests;

public class CreateGroupRequest
{
    public required string Name { get; init; }
    public required string Currency { get; init; }
}