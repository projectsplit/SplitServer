using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Queries;

public class GetFactorsQueryHandler : IRequestHandler<GetFactorsQuery, Result<FactorsResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public GetFactorsQueryHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<FactorsResponse>> Handle(GetFactorsQuery query, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(query.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<FactorsResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        try
        {
            var response = await _riskEngineClient.GetFactorsAsync(runId);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<FactorsResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
