using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteTransferCommand : IRequest<Result>
{
    public string UserId { get; }
    public string TransferId { get; }

    public DeleteTransferCommand(
        string userId,
        string transferId)
    {
        UserId = userId;
        TransferId = transferId;
    }
}