using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Mappers;

public class CurrencyExchangeRatesMapper : IMapper<CurrencyExchangeRates, CurrencyExchangeRatesMongoDbDocument>
{
    public CurrencyExchangeRates ToEntity(CurrencyExchangeRatesMongoDbDocument document)
    {
        return new CurrencyExchangeRates
        {
            Id = document.Id,
            IsDeleted = document.IsDeleted,
            Created = document.Created,
            Updated = document.Updated,
            Base = document.Base,
            Date = DateOnly.Parse(document.Date),
            Rates = document.Rates,
        };
    }

    public CurrencyExchangeRatesMongoDbDocument ToDocument(CurrencyExchangeRates entity)
    {
        return new CurrencyExchangeRatesMongoDbDocument
        {
            Id = entity.Id,
            IsDeleted = entity.IsDeleted,
            Created = entity.Created,
            Updated = entity.Updated,
            Base = entity.Base,
            Date = entity.Date.ToString("O"),
            Rates = entity.Rates,

        };
    }
}