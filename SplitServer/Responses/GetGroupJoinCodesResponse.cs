using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupJoinCodesResponse
{
    public required List<JoinCode> Codes { get; init; }
    public required string? Next { get; init; }
}