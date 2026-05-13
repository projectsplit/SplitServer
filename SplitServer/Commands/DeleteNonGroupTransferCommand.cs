using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteNonGroupTransferCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string TransferId { get; init; }
}