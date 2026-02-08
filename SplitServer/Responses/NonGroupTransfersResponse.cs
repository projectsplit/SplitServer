namespace SplitServer.Responses;

public class NonGroupTransfersResponse
{
    public required List<NonGroupTransferResponseItem> Transfers { get; init; }
    public required string? Next { get; init; }
}