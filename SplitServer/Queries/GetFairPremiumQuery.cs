using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Queries;

public class GetFairPremiumQuery : IRequest<Result<FairPremiumResponse>>
{
    public required string UserId { get; init; }
    public required string RiskName { get; init; }
    public double? MaxLoss { get; init; }
}
