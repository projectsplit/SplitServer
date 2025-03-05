using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class CreateTransferCommand : IRequest<Result<CreateTransferResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occured { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
}