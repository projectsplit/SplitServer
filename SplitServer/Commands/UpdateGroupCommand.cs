using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class UpdateGroupCommand : IRequest<Result>
{
    public string UserId { get; }
    
    public string GroupId { get; }
    
    public string Name { get; }
    
    public string Currency { get; }

    public UpdateGroupCommand(
        string userId,
        string groupId,
        string name,
        string currency)
    {
        UserId = userId;
        GroupId = groupId;
        Name = name;
        Currency = currency;
    }
}