using SplitServer.Models;

namespace SplitServer.Extensions;

public static class DecimalExtensions
{
    public static decimal Convert(
        this decimal sourceAmount,
        string sourceCurrency,
        CurrencyExchangeRates rates,
        string targetCurrency)
    {
        var sourceRate = rates.Rates[sourceCurrency];

        var usdAmount = sourceAmount / sourceRate;

        var targetRate = rates.Rates[targetCurrency];

        return usdAmount * targetRate;
    }
}