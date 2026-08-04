using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SubscribeToPushCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Endpoint { get; init; }
    public required string P256dh { get; init; }
    public required string Auth { get; init; }
}
