using SplitServer.Commands;

namespace SplitServer.Requests;

public class CreateManyNonGroupTransfersRequest
{
    public required List<CreateManyTransfersItem> Transfers { get; init; }
}