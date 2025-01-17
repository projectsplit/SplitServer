using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class CreateInvitationCommand : IRequest<Result>
{
    public string FromId { get; }
    public string ToId { get; }
    public string GroupId { get; }

    public CreateInvitationCommand(
        string fromId,
        string toId,
        string groupId)
    {
        FromId = fromId;
        GroupId = groupId;
        ToId = toId;
    }
}