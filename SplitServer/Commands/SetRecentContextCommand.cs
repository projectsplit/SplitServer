using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetRecentContextCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string ContextId { get; init; }
}