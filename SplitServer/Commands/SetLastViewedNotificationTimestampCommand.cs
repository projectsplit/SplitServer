using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetLastViewedNotificationTimestampCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required DateTime Timestamp { get; init; }
}