using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class AcceptInvitationCommand : IRequest<Result>
{
    public string UserId { get; }
    public string InvitationId { get; }

    public AcceptInvitationCommand(string userId, string invitationId)
    {
        UserId = userId;
        InvitationId = invitationId;
    }
}