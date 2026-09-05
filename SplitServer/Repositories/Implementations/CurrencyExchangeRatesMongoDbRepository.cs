using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class CurrencyExchangeRatesMongoDbRepository :
    MongoDbRepositoryBase<CurrencyExchangeRates, CurrencyExchangeRatesMongoDbDocument>,
    ICurrencyExchangeRatesRepository
{
    public CurrencyExchangeRatesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "CurrencyExchangeRates",
            new CurrencyExchangeRatesMapper())
    {
    }

    public async Task<Maybe<CurrencyExchangeRates>> GetByDate(DateOnly date, CancellationToken ct)
    {
        var filter = FilterBuilder.Eq(x => x.Date, date.ToString("O"));

        var document = await Collection.Find(filter).FirstOrDefaultAsync(ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe.None;
    }

    public async Task<Maybe<CurrencyExchangeRates>> GetLatest(CancellationToken ct)
    {
        var document = await Collection
            .Find(FilterBuilder.Empty)
            .SortByDescending(x => x.Date)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe.None;
    }

    // Date is persisted as an "O" formatted string, which for DateOnly is yyyy-MM-dd. Comparing and
    // sorting those as text gives the same order as comparing the dates, so the range filters below
    // and the sorts above are chronological.
    public async Task<List<CurrencyExchangeRates>> GetByDates(IReadOnlyCollection<DateOnly> dates, CancellationToken ct)
    {
        if (dates.Count == 0)
        {
            return [];
        }

        var filter = FilterBuilder.In(x => x.Date, dates.Select(x => x.ToString("O")));

        var documents = await Collection
            .Find(filter)
            .SortBy(x => x.Date)
            .ToListAsync(ct);

        return documents.Select(Mapper.ToEntity).ToList();
    }

    public async Task<Maybe<CurrencyExchangeRates>> GetLatestOnOrBefore(DateOnly date, CancellationToken ct)
    {
        var filter = FilterBuilder.Lte(x => x.Date, date.ToString("O"));

        var document = await Collection
            .Find(filter)
            .SortByDescending(x => x.Date)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return document is not null
            ? Mapper.ToEntity(document)
            : Maybe.None;
    }
}