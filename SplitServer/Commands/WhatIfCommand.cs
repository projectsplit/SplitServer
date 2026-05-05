using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class WhatIfCommand : IRequest<Result<WhatIfResponse>>
{
    public required string UserId { get; init; }
    public double BufferDelta { get; init; }
    public double ExpenseCut { get; init; }
    public double SalaryDelta { get; init; }
    public ReweightRequest? Reweight { get; init; }
    public Dictionary<string, double> DisabledRisks { get; init; } = new();
    public Dictionary<string, double[]> RiskCaps { get; init; } = new();
    public bool ExcludeProperty { get; init; }
}
