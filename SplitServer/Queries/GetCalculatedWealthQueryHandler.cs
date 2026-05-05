using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetCalculatedWealthQueryHandler : IRequestHandler<GetCalculatedWealthQuery, Result<GetCalculatedWealthResponse>>
{
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public GetCalculatedWealthQueryHandler(ICalculatedWealthRepository calculatedWealthRepository)
    {
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<GetCalculatedWealthResponse>> Handle(GetCalculatedWealthQuery query, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(query.UserId, ct);

        if (wealthMaybe.HasNoValue)
        {
            return Result.Failure<GetCalculatedWealthResponse>("No calculated wealth found for this user");
        }

        var wealth = wealthMaybe.Value;

        return new GetCalculatedWealthResponse
        {
            RunId = wealth.RunId,
            StartingWealth = wealth.StartingWealth,
            Economy = wealth.Economy,
            Summary = wealth.Summary,
            Scenarios = wealth.Scenarios,
            NSims = wealth.NSims,
            RealizedCorrelation = wealth.RealizedCorrelation
        };
    }
}
