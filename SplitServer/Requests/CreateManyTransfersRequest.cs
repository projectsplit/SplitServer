using SplitServer.Commands;

namespace SplitServer.Requests;

public class CreateManyTransfersRequest
{
    public required string GroupId { get; init; }
    public required List<CreateManyTransfersItem> Transfers { get; init; }
}