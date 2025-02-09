using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class UpdateGroupCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
}