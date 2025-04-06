using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class EditGroupNameCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string Name { get; init; }
}