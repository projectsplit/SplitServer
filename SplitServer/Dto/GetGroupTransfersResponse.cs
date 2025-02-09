using SplitServer.Models;

namespace SplitServer.Dto;

public class GetGroupTransfersResponse
{
    public required List<Transfer> Transfers { get; init; }
    public required string? Next { get; init; }
}