
namespace SplitServer.Responses;

public class GroupTransfersResponse
{
    public required List<GroupTransferResponseItem> Transfers { get; init; }
    public required string? Next { get; init; }
}