using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class AcceptConnectionRequestCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string ConnectionId { get; init; }
}
