﻿using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.OpenExchangeRates;

namespace SplitServer.Services.CurrencyExchangeRate;

public class CurrencyExchangeRateService
{
    private readonly OpenExchangeRatesClient _openExchangeRatesClient;
    private readonly ICurrencyExchangeRatesRepository _currencyExchangeRatesRepository;
    private const string BaseCurrency = "USD";

    public CurrencyExchangeRateService(
        OpenExchangeRatesClient openExchangeRatesClient,
        ICurrencyExchangeRatesRepository currencyExchangeRatesRepository)
    {
        _openExchangeRatesClient = openExchangeRatesClient;
        _currencyExchangeRatesRepository = currencyExchangeRatesRepository;
    }

    public decimal Convert(decimal sourceAmount, string sourceCurrency, CurrencyExchangeRates rates, string targetCurrency)
    {
        if (sourceCurrency == targetCurrency)
        {
            return sourceAmount;
        }

        var rate = rates.Rates[sourceCurrency] / rates.Rates[targetCurrency];

        return sourceAmount / rate;
    }

    public async Task<Result<CurrencyExchangeRates>> GetLatestStoredRates(CancellationToken ct)
    {
        var ratesMaybe = await _currencyExchangeRatesRepository.GetLatest(ct);

        if (ratesMaybe.HasNoValue)
        {
            return Result.Failure<CurrencyExchangeRates>("No currency exchange rates found");
        }

        return ratesMaybe.Value;
    }

    public async Task<Result<CurrencyExchangeRates>> GetStoredOrStoreRates(DateOnly date, CancellationToken ct)
    {
        var storedRates = await _currencyExchangeRatesRepository.GetByDate(date, ct);

        if (storedRates.HasValue)
        {
            return storedRates.Value;
        }

        var fetchRatesResult = await _openExchangeRatesClient.GetHistoricalRates(BaseCurrency, date, ct);

        if (fetchRatesResult.IsFailure)
        {
            return fetchRatesResult.ConvertFailure<CurrencyExchangeRates>();
        }

        var fetchedRates = fetchRatesResult.Value;

        var now = DateTime.Now;

        var ratesEntity = new CurrencyExchangeRates
        {
            Id = $"{date.ToString("O")}_{BaseCurrency}",
            Created = now,
            Updated = now,
            Base = BaseCurrency,
            Date = date,
            Rates = fetchedRates,
        };

        var writeRates = await _currencyExchangeRatesRepository.Insert(ratesEntity, ct);

        if (writeRates.IsFailure)
        {
            return writeRates.ConvertFailure<CurrencyExchangeRates>();
        }

        return ratesEntity;
    }
}