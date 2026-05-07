using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ConditionalSweepCommand : IRequest<Result<ConditionalSweepResponse>>
{
    public required string UserId { get; init; }
    public required string Factor { get; init; }
    public required string Op { get; init; }
    public List<double>? Thresholds { get; init; }
    public List<double>? AutoQuantiles { get; init; }
}
