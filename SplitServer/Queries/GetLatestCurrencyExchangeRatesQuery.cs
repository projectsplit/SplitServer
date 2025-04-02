using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;

namespace SplitServer.Queries;

public class GetLatestCurrencyExchangeRatesQuery : IRequest<Result<CurrencyExchangeRates>>;
