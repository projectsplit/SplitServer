using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;

namespace SplitServer.Commands;

public class SubscribeToPushCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Endpoint { get; init; }
    public required PushDeviceKind Kind { get; init; }
    public string? P256dh { get; init; }
    public string? Auth { get; init; }
}
