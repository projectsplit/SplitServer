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
}