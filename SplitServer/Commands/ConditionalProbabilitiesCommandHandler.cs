using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Commands;

public class ConditionalProbabilitiesCommandHandler : IRequestHandler<ConditionalProbabilitiesCommand, Result<ConditionalQueryResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public ConditionalProbabilitiesCommandHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<ConditionalQueryResponse>> Handle(ConditionalProbabilitiesCommand command, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(command.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<ConditionalQueryResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        var request = new ConditionalQueryRequest
        {
            Conditions = command.Conditions
        };

        try
        {
            var response = await _riskEngineClient.ConditionalProbabilitiesAsync(runId, request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<ConditionalQueryResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
