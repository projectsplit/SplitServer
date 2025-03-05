using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupJoinTokensResponse
{
    public required List<JoinToken> JoinTokens { get; init; }
    public required string? Next { get; init; }
}