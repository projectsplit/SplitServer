using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class CreateManyTransfersCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required List<CreateManyTransfersItem> Transfers { get; init; }
}