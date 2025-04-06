using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RemoveGroupGuestCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string GuestId { get; init; }
}