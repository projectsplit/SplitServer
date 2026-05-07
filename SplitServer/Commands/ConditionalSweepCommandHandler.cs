using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Commands;

public class ConditionalSweepCommandHandler : IRequestHandler<ConditionalSweepCommand, Result<ConditionalSweepResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public ConditionalSweepCommandHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<ConditionalSweepResponse>> Handle(ConditionalSweepCommand command, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(command.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<ConditionalSweepResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        var request = new ConditionalSweepRequest
        {
            Factor = command.Factor,
            Op = command.Op,
            Thresholds = command.Thresholds,
            AutoQuantiles = command.AutoQuantiles
        };

        try
        {
            var response = await _riskEngineClient.ConditionalSweepProbabilitiesAsync(runId, request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<ConditionalSweepResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
