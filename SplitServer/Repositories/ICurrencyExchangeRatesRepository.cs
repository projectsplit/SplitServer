using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ICurrencyExchangeRatesRepository : IRepositoryBase<CurrencyExchangeRates>
{
    Task<Maybe<CurrencyExchangeRates>> GetByDate(DateOnly date, CancellationToken ct);

    Task<Maybe<CurrencyExchangeRates>> GetLatest(CancellationToken ct);

    /// <summary>
    /// The quotes for exactly these days. Asked for the days that actually carry expenses rather
    /// than a whole window, because a quote holds every supported currency and a year of them is
    /// megabytes to pull in for a handful of conversions.
    /// </summary>
    Task<List<CurrencyExchangeRates>> GetByDates(IReadOnlyCollection<DateOnly> dates, CancellationToken ct);

    /// <summary>
    /// The most recent quote no later than the given date. Carries a rate forward over a day the
    /// collection job missed, and anchors expenses that predate the window being loaded.
    /// </summary>
    Task<Maybe<CurrencyExchangeRates>> GetLatestOnOrBefore(DateOnly date, CancellationToken ct);
}