namespace SplitServer.Responses;

public class GetGroupJoinCodesResponse
{
    public required List<JoinCodeResponseItem> Codes { get; init; }
    public required string? Next { get; init; }
}