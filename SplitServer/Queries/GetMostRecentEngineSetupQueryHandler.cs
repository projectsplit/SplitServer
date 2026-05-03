using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetMostRecentEngineSetupQueryHandler : IRequestHandler<GetMostRecentEngineSetupQuery, Result<GetMostRecentEngineSetupResponse>>
{
    private readonly IRiskEngineRepository _riskEngineRepository;

    public GetMostRecentEngineSetupQueryHandler(IRiskEngineRepository riskEngineRepository)
    {
        _riskEngineRepository = riskEngineRepository;
    }

    public async Task<Result<GetMostRecentEngineSetupResponse>> Handle(GetMostRecentEngineSetupQuery query, CancellationToken ct)
    {
        var setupMaybe = await _riskEngineRepository.GetByUserId(query.UserId, ct);

        if (setupMaybe.HasNoValue)
        {
            return Result.Failure<GetMostRecentEngineSetupResponse>("No simulation setup found for this user");
        }

        var setup = setupMaybe.Value;

        return new GetMostRecentEngineSetupResponse
        {
            Economy = setup.Economy,
            Financials = setup.Financials,
            RiskToggles = setup.RiskToggles,
            CustomRisks = setup.CustomRisks,
            Correlations = setup.Correlations
        };
    }
}
