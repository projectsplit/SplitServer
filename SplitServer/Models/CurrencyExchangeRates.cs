namespace SplitServer.Models;

public record CurrencyExchangeRates : EntityBase
{
    public required string Base { get; init; }
    public required DateOnly Date { get; init; }
    public required IDictionary<string, decimal> Rates { get; init; }
}