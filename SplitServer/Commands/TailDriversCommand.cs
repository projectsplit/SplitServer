using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class TailDriversCommand : IRequest<Result<TailDriversResponse>>
{
    public required string UserId { get; init; }
    public bool ExcludeProperty { get; init; }
    public int TailThresholdBusts { get; init; } = 50;
    public double TailFallbackPct { get; init; } = 0.5;
    public double PairQuantile { get; init; } = 0.25;
    public int PairTopN { get; init; } = 10;
    public int PathDepth { get; init; } = 3;
    public int PathTopN { get; init; } = 5;
}
