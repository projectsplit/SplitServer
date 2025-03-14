using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetRecentGroupCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}