using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class EditGroupArchiveStatusCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required bool IsArchived { get; init; }
}