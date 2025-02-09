using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteTransferCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string TransferId { get; init; }
}