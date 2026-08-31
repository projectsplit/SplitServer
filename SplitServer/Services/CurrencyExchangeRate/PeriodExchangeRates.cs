using SplitServer.Models;

namespace SplitServer.Services.CurrencyExchangeRate;

/// <summary>
/// The daily quotes covering a reporting period, so an expense is converted at what its currency
/// was worth on the day it happened rather than at whatever today's rate is.
/// <para>
/// This is for reporting on spending that already happened. A live balance is the opposite case —
/// what someone owes right now is worth today's rate, not the rate on the day the debt arose — so
/// debts and balances deliberately keep using the latest quote.
/// </para>
/// </summary>
public class PeriodExchangeRates
{
    private readonly List<CurrencyExchangeRates> _ascendingByDate;
    private readonly CurrencyExchangeRates _mostRecent;
    private readonly Dictionary<DateOnly, CurrencyExchangeRates> _resolvedByDate = new();

    public PeriodExchangeRates(IEnumerable<CurrencyExchangeRates> ratesInPeriod, CurrencyExchangeRates mostRecent)
    {
        _ascendingByDate = ratesInPeriod
            .GroupBy(x => x.Date)
            .Select(x => x.First())
            .OrderBy(x => x.Date)
            .ToList();

        _mostRecent = mostRecent;
    }

    public decimal Convert(decimal sourceAmount, string sourceCurrency, string targetCurrency, DateOnly on)
    {
        if (sourceCurrency == targetCurrency)
        {
            return sourceAmount;
        }

        var rates = RatesOn(on);

        // A quote from years ago can predate a currency the app supports today. Converting at the
        // current rate is a better answer than failing the whole report over one expense.
        if (!rates.Rates.ContainsKey(sourceCurrency) || !rates.Rates.ContainsKey(targetCurrency))
        {
            rates = _mostRecent;
        }

        var rate = rates.Rates[sourceCurrency] / rates.Rates[targetCurrency];

        return sourceAmount / rate;
    }

    /// <summary>
    /// The quote in force on a given day: the one published that day, or the most recent one before
    /// it if the collection job missed a run. Only when a day precedes every quote we hold does it
    /// fall through to the latest, which is the old behaviour and the best available guess.
    /// </summary>
    private CurrencyExchangeRates RatesOn(DateOnly date)
    {
        if (_resolvedByDate.TryGetValue(date, out var alreadyResolved))
        {
            return alreadyResolved;
        }

        var resolved = _ascendingByDate.LastOrDefault(x => x.Date <= date) ?? _mostRecent;

        _resolvedByDate[date] = resolved;

        return resolved;
    }
}
