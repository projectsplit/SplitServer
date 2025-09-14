using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetSpendingsChartQueryHandler : IRequestHandler<GetSpendingsChartQuery, Result<GetSpendingsChartResponse>>
{
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;

    public GetSpendingsChartQueryHandler(
        IUserPreferencesRepository userPreferencesRepository,
        IExpensesRepository expensesRepository,
        IGroupsRepository groupsRepository,
        CurrencyExchangeRateService currencyExchangeRateService)
    {
        _userPreferencesRepository = userPreferencesRepository;
        _expensesRepository = expensesRepository;
        _groupsRepository = groupsRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
    }

    public async Task<Result<GetSpendingsChartResponse>> Handle(GetSpendingsChartQuery query, CancellationToken ct)
    {
        if (!Enum.TryParse(query.Granularity, true, out Granularity granularity))
        {
            return Result.Failure<GetSpendingsChartResponse>(
                $"Invalid granularity. ({string.Join(", ", Enum.GetNames(typeof(Granularity)).Select(x => x.ToLowerInvariant()))})");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var userCurrency = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.Currency ?? DefaultValues.Currency
            : DefaultValues.Currency;

        var currencyRatesResult = await _currencyExchangeRateService.GetLatestStoredRates(ct);
        if (currencyRatesResult.IsFailure)
        {
            return currencyRatesResult.ConvertFailure<GetSpendingsChartResponse>();
        }

        var rates = currencyRatesResult.Value;

        var utcStartDate = query.StartDate.ToUtc(userTimeZoneId);
        var utcEndDate = query.EndDate.EndOfDay().ToUtc(userTimeZoneId);

        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var membersByGroup = groups.ToDictionary(x => x.Id, x => x.Members.First(m => m.UserId == query.UserId));
        var memberIds = membersByGroup.Select(m => m.Value.Id).ToList();

        var expenses = await _expensesRepository.GetAllByMemberIds(memberIds, utcStartDate, utcEndDate, ct);

        var currentUtcDateTime = utcStartDate;
        var currentUserDate = query.StartDate;
        var shareSumSoFar = 0m;
        var paymentSumSoFar = 0m;
        var responseItems = new List<GetSpendingsChartResponseItem>();

        while (currentUtcDateTime <= utcEndDate)
        {
            var timeIncrement = granularity is Granularity.Daily
                ? TimeSpan.FromDays(1)
                : TimeSpan.FromDays(DateTime.DaysInMonth(currentUserDate.Year, currentUserDate.Month));

            var shareSum = expenses
                .Where(x => x.Occurred >= currentUtcDateTime && x.Occurred < currentUtcDateTime + timeIncrement)
                .Sum(x =>
                {
                    var shareAmount = x.Shares.FirstOrDefault(s => memberIds.Contains(s.MemberId))?.Amount ?? 0;
                    return _currencyExchangeRateService.Convert(shareAmount, x.Currency, rates, userCurrency);
                });

            var paymentSum = expenses
                .Where(x => x.Occurred >= currentUtcDateTime && x.Occurred < currentUtcDateTime + timeIncrement)
                .Sum(x =>
                {
                    var paymentAmount = x.Payments.FirstOrDefault(s => memberIds.Contains(s.MemberId))?.Amount ?? 0;
                    return _currencyExchangeRateService.Convert(paymentAmount, x.Currency, rates, userCurrency);
                });

            shareSumSoFar += shareSum;
            paymentSumSoFar += paymentSum;

            var responseItem = new GetSpendingsChartResponseItem
            {
                ShareAmount = shareSum,
                AccumulativeShareAmount = shareSumSoFar,
                PaymentAmount = paymentSum,
                AccumulativePaymentAmount = paymentSumSoFar,
                From = currentUserDate,
                To = currentUserDate + timeIncrement - TimeSpan.FromTicks(1)
            };

            responseItems.Add(responseItem);

            currentUserDate += timeIncrement;
            currentUtcDateTime = currentUserDate.ToUtc(userTimeZoneId);
        }

        return new GetSpendingsChartResponse
        {
            Items = responseItems
        };
    }
}