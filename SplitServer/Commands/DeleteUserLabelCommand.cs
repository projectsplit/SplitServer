using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteUserLabelCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string LabelId { get; init; }
}