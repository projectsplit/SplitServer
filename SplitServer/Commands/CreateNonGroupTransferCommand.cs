using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class CreateNonGroupTransferCommand : IRequest<Result<CreateTransferResponse>>
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }

    /// <summary>The id the app picked for this transfer, if it picked one. See <see cref="Services.ClientIds"/>.</summary>
    public string? TransferId { get; init; }
}