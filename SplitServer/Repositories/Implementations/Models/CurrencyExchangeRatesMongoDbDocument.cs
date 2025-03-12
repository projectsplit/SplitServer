using SplitServer.Models;

namespace SplitServer.Repositories.Implementations.Models;

public record CurrencyExchangeRatesMongoDbDocument : EntityBase
{
    public required string Base { get; init; }
    public required string Date { get; init; }
    public required IDictionary<string, decimal> Rates { get; init; }
}