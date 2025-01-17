using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class DeleteGroupCommand : IRequest<Result>
{
    public string UserId { get; }
    
    public string GroupId { get; }

    public DeleteGroupCommand(
        string userId,
        string groupId)
    {
        UserId = userId;
        GroupId = groupId;
    }
}