using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;

namespace SplitServer.Commands;

public class AddGuestCommand : IRequest<Result<Guest>>
{
    
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string GuestName { get; init; }
}