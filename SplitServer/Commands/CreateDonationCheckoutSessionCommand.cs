using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class CreateDonationCheckoutSessionCommand : IRequest<Result<CreateDonationCheckoutSessionResponse>>
{
    public required string UserId { get; init; }
    public required long AmountMinor { get; init; }
    public required bool Monthly { get; init; }
}
