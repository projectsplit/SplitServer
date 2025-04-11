using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RemoveGroupLabelCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string LabelId { get; init; }
}