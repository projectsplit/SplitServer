using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Commands;

public class StoreHistoricalRatesCommandHandler : IRequestHandler<StoreHistoricalRatesCommand, Result<CurrencyExchangeRates>>
{
    private readonly CurrencyExchangeRateService _currencyExchangeRatesService;

    public StoreHistoricalRatesCommandHandler(
        CurrencyExchangeRateService currencyExchangeRatesService)
    {
        _currencyExchangeRatesService = currencyExchangeRatesService;
    }

    public async Task<Result<CurrencyExchangeRates>> Handle(StoreHistoricalRatesCommand command, CancellationToken ct)
    {
        DateOnly date;
        var now = DateTime.UtcNow;

        if (command.Date is not null)
        {
            if (!DateOnly.TryParse(command.Date, out var parsedDate))
            {
                return Result.Failure<CurrencyExchangeRates>("Date format is invalid");
            }

            date = parsedDate;
        }
        else
        {
            var yesterday = DateOnly.FromDateTime(now - TimeSpan.FromDays(1));
            date = yesterday;
        }

        if (date >= DateOnly.FromDateTime(now))
        {
            return Result.Failure<CurrencyExchangeRates>("Date should be in the past");
        }

        return await _currencyExchangeRatesService.GetStoredOrStoreRates(date, ct);
    }
}