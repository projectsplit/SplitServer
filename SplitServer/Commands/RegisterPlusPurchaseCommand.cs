using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RegisterPlusPurchaseCommand : IRequest<Result>
{
    public required string UserId { get; init; }

    /// <summary>Play's receipt for the subscription. Meaningless until Google is asked about it.</summary>
    public required string PurchaseToken { get; init; }
}
