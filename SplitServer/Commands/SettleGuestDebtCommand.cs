using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SettleGuestDebtCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string GuestId { get; init; }
}