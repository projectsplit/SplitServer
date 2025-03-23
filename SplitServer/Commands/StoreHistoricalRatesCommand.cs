using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;

namespace SplitServer.Commands;

public class StoreHistoricalRatesCommand : IRequest<Result<CurrencyExchangeRates>>
{
    public required string? Date { get; init; }
}