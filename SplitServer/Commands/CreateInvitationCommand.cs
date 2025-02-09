using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class CreateInvitationCommand : IRequest<Result>
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required string GroupId { get; init; }
}