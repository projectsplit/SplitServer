using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Queries;

public class GetFairPremiumQueryHandler : IRequestHandler<GetFairPremiumQuery, Result<FairPremiumResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public GetFairPremiumQueryHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<FairPremiumResponse>> Handle(GetFairPremiumQuery query, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(query.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<FairPremiumResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        var request = new FairPremiumRequest
        {
            RiskName = query.RiskName,
            MaxLoss = query.MaxLoss
        };

        try
        {
            var response = await _riskEngineClient.GetFairPremiumAsync(runId, request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<FairPremiumResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
