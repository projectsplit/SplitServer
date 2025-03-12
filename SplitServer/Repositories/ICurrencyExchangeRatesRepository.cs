using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ICurrencyExchangeRatesRepository : IRepositoryBase<CurrencyExchangeRates>
{
    Task<Maybe<CurrencyExchangeRates>> GetByDate(DateOnly date, CancellationToken ct);
}