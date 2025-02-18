using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SendInvitationCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string ReceiverId { get; init; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }
}