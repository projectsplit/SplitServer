using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class UnsubscribeFromPushCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Endpoint { get; init; }
}
