using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetLatestCurrencyExchangeRatesQueryHandler :
    IRequestHandler<GetLatestCurrencyExchangeRatesQuery, Result<CurrencyExchangeRates>>
{
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;

    public GetLatestCurrencyExchangeRatesQueryHandler(CurrencyExchangeRateService currencyExchangeRateService)
    {
        _currencyExchangeRateService = currencyExchangeRateService;
    }

    public async Task<Result<CurrencyExchangeRates>> Handle(GetLatestCurrencyExchangeRatesQuery query, CancellationToken ct)
    {
        return await _currencyExchangeRateService.GetLatestStoredRates(ct);
    }
}