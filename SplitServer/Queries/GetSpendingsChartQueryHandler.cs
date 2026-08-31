using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetSpendingsChartQueryHandler : IRequestHandler<GetSpendingsChartQuery, Result<GetSpendingsChartResponse>>
{
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly ValidationService _validationService;

    public GetSpendingsChartQueryHandler(
        IUserPreferencesRepository userPreferencesRepository,
        IExpensesRepository expensesRepository,
        IGroupsRepository groupsRepository,
        CurrencyExchangeRateService currencyExchangeRateService,
        ValidationService validationService)
    {
        _userPreferencesRepository = userPreferencesRepository;
        _expensesRepository = expensesRepository;
        _groupsRepository = groupsRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
        _validationService = validationService;
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

        var currencyResult = _validationService.ValidateCurrency(query.Currency);

        if (currencyResult.IsFailure)
        {
            return currencyResult.ConvertFailure<GetSpendingsChartResponse>();
        }

        var currency = currencyResult.Value;

        var utcStartDate = query.StartDate.ToUtc(userTimeZoneId);
        var utcEndDate = query.EndDate.EndOfDay().ToUtc(userTimeZoneId);

        // Current membership only. Leaving a group hands the member id to a guest the group owns
        // from then on, and the expenses behind it stop counting towards the person who left —
        // reclaiming that guest slot on the way back in is what makes them count again.
        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var memberIds = groups.SelectMany(g => g.Members.Where(m => m.UserId == query.UserId).Select(m => m.Id)).ToList();

        var groupExpenses = await _expensesRepository.GetGroupExpensesByMemberIds(memberIds, utcStartDate, utcEndDate, ct);
        var nonGroupExpenses = await _expensesRepository.GetNonGroupExpensesByUserId(query.UserId, utcStartDate, utcEndDate, ct);
        var personalExpenses = await _expensesRepository.GetPersonalExpensesByUserId(query.UserId, memberIds, ct, utcStartDate, utcEndDate);

        // Rates are loaded after the expenses so only the days that carry one are asked for.
        var expenseDates = groupExpenses.Select(x => x.Occurred)
            .Concat(nonGroupExpenses.Select(x => x.Occurred))
            .Concat(personalExpenses.Select(x => x.Occurred))
            .Select(DateOnly.FromDateTime)
            .Distinct()
            .ToList();

        var currencyRatesResult = await _currencyExchangeRateService.GetRatesForDates(expenseDates, ct);

        if (currencyRatesResult.IsFailure)
        {
            return currencyRatesResult.ConvertFailure<GetSpendingsChartResponse>();
        }

        var rates = currencyRatesResult.Value;

        var currentUtcDateTime = utcStartDate;
        var currentUserDate = query.StartDate;
        var shareSumSoFar = 0m;
        var paymentSumSoFar = 0m;
        var lentSumSoFar = 0m;
        var borrowedSumSoFar = 0m;
        var responseItems = new List<GetSpendingsChartResponseItem>();

        while (currentUtcDateTime <= utcEndDate)
        {
            // The next boundary is stepped in the user's own calendar and only then converted, so
            // consecutive buckets meet exactly. Adding a fixed 24 hours to the UTC instant instead
            // would disagree with where the following bucket starts on the days a clock change
            // makes 23 or 25 hours long, dropping an hour of expenses in autumn and counting one
            // twice in spring.
            var nextUserDate = granularity is Granularity.Daily
                ? currentUserDate.AddDays(1)
                : currentUserDate.AddMonths(1);

            var bucketEnd = nextUserDate.ToUtc(userTimeZoneId);

            // Each expense keeps its share and payment paired up, because lending and borrowing are
            // decided per expense. Summing shares and payments separately first and subtracting
            // would cancel a covered expense against one the user fronted in the same bucket,
            // understating both sides and making the result depend on the chosen granularity.
            var groupAmounts = groupExpenses
                .Where(x => x.Occurred >= currentUtcDateTime && x.Occurred < bucketEnd)
                .Select(x => (
                    Share: rates.Convert(
                        x.Shares.FirstOrDefault(s => memberIds.Contains(s.MemberId))?.Amount ?? 0,
                        x.Currency,
                        query.Currency,
                        DateOnly.FromDateTime(x.Occurred)),
                    Payment: rates.Convert(
                        x.Payments.FirstOrDefault(p => memberIds.Contains(p.MemberId))?.Amount ?? 0,
                        x.Currency,
                        query.Currency,
                        DateOnly.FromDateTime(x.Occurred))));

            var nonGroupAmounts = nonGroupExpenses
                .Where(x => x.Occurred >= currentUtcDateTime && x.Occurred < bucketEnd)
                .Select(x => (
                    Share: rates.Convert(
                        x.Shares.FirstOrDefault(s => s.UserId == query.UserId)?.Amount ?? 0,
                        x.Currency,
                        query.Currency,
                        DateOnly.FromDateTime(x.Occurred)),
                    Payment: rates.Convert(
                        x.Payments.FirstOrDefault(p => p.UserId == query.UserId)?.Amount ?? 0,
                        x.Currency,
                        query.Currency,
                        DateOnly.FromDateTime(x.Occurred))));

            var sharedAmounts = groupAmounts.Concat(nonGroupAmounts).ToList();

            var personalExpensesSum = personalExpenses.OfType<PersonalExpense>()
                .Where(x => x.Occurred >= currentUtcDateTime && x.Occurred < bucketEnd)
                .Sum(x => rates.Convert(x.Amount, x.Currency, query.Currency, DateOnly.FromDateTime(x.Occurred)));

            // Personal expenses belong in what the user spent, so they are in the share sum and in
            // its running total both. Keeping them out of one and not the other used to mean the
            // accumulative figure was not the running sum of the per-bucket one.
            var shareSum = sharedAmounts.Sum(x => x.Share) + personalExpensesSum;
            var paymentSum = sharedAmounts.Sum(x => x.Payment);

            // Personal expenses are the user's own money on themselves, so they are neither lent
            // nor borrowed and are deliberately left out of both sums.
            var lentSum = sharedAmounts.Sum(x => Math.Max(0m, x.Payment - x.Share));
            var borrowedSum = sharedAmounts.Sum(x => Math.Max(0m, x.Share - x.Payment));

            shareSumSoFar += shareSum;
            paymentSumSoFar += paymentSum;
            lentSumSoFar += lentSum;
            borrowedSumSoFar += borrowedSum;

            var responseItem = new GetSpendingsChartResponseItem
            {
                ShareAmount = Math.Round(shareSum, currency.SignificantDecimalDigits),
                AccumulativeShareAmount = Math.Round(shareSumSoFar, currency.SignificantDecimalDigits),
                PaymentAmount = Math.Round(paymentSum, currency.SignificantDecimalDigits),
                AccumulativePaymentAmount = Math.Round(paymentSumSoFar, currency.SignificantDecimalDigits),
                LentAmount = Math.Round(lentSum, currency.SignificantDecimalDigits),
                AccumulativeLentAmount = Math.Round(lentSumSoFar, currency.SignificantDecimalDigits),
                BorrowedAmount = Math.Round(borrowedSum, currency.SignificantDecimalDigits),
                AccumulativeBorrowedAmount = Math.Round(borrowedSumSoFar, currency.SignificantDecimalDigits),
                From = currentUserDate,
                To = nextUserDate - TimeSpan.FromTicks(1)
            };

            responseItems.Add(responseItem);

            currentUserDate = nextUserDate;
            currentUtcDateTime = bucketEnd;
        }

        return new GetSpendingsChartResponse
        {
            Items = responseItems
        };
    }
}