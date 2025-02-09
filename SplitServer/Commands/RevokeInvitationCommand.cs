using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RevokeInvitationCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string InvitationId { get; init; }
}