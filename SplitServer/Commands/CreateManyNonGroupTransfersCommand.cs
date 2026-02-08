using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class CreateManyNonGroupTransfersCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required List<CreateManyTransfersItem> Transfers { get; init; }
}