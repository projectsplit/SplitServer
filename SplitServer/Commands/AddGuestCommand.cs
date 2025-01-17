using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class AddGuestCommand : IRequest<Result>
{
    public string UserId { get; }
    
    public string GroupId { get; }
    
    public string GuestName { get; }
    
    public AddGuestCommand(
        string userId,
        string groupId,
        string guestName)
    {
        UserId = userId;
        GroupId = groupId;
        GuestName = guestName;
    }
}