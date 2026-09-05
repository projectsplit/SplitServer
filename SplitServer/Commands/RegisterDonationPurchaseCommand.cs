using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RegisterDonationPurchaseCommand : IRequest<Result>
{
    public required string UserId { get; init; }

    public required string ProductId { get; init; }

    /// <summary>Play's receipt for the purchase. Meaningless until Google is asked about it.</summary>
    public required string PurchaseToken { get; init; }
}
