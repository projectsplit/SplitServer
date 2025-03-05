using SplitServer.Models;

namespace SplitServer.Responses;

public class GetGroupTransfersResponse
{
    public required List<Transfer> Transfers { get; init; }
    public required string? Next { get; init; }
}