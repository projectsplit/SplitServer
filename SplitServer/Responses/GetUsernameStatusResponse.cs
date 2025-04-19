namespace SplitServer.Responses;

public class GetUsernameStatusResponse
{
    public required bool IsValid { get; init; }
    public required string? ErrorMessage { get; init; }
    public required bool IsAvailable { get; init; }
}