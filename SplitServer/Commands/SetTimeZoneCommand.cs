using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetTimeZoneCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string TimeZone { get; init; }
}