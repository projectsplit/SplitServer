using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Commands;

public class TailDriversCommandHandler : IRequestHandler<TailDriversCommand, Result<TailDriversResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public TailDriversCommandHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<TailDriversResponse>> Handle(TailDriversCommand command, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(command.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<TailDriversResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        var request = new TailDriversRequest
        {
            ExcludeProperty = command.ExcludeProperty,
            TailThresholdBusts = command.TailThresholdBusts,
            TailFallbackPct = command.TailFallbackPct,
            PairQuantile = command.PairQuantile,
            PairTopN = command.PairTopN,
            PathDepth = command.PathDepth,
            PathTopN = command.PathTopN
        };

        try
        {
            var response = await _riskEngineClient.TailRiskDriversAsync(runId, request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<TailDriversResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
