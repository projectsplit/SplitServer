using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Services;

public class BudgetService
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;

    public BudgetService(
        IExpensesRepository expensesRepository,
        IGroupsRepository groupsRepository,
        CurrencyExchangeRateService currencyExchangeRateService)
    {
        _expensesRepository = expensesRepository;
        _groupsRepository = groupsRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
    }

    public Result<(DateTime startDate, DateTime endDate)> CalculateDates(Budget budget)
    {
        var now = DateTime.UtcNow.Date;

        if (budget.Frequency == BudgetFrequency.Custom)
        {
            if (budget.StartDate is null || budget.EndDate is null)
            {
                return Result.Failure<(DateTime startDate, DateTime endDate)>("Start date and end date must be provided for custom frequency");
            }

            return (budget.StartDate.Value.Date, budget.EndDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (budget.Frequency == BudgetFrequency.Weekly)
        {
            if (!Enum.TryParse<DayOfWeek>(budget.CommencementDay, true, out var targetDay))
            {
                return Result.Failure<(DateTime startDate, DateTime endDate)>("Invalid commencement day for weekly budget");
            }

            var diff = (int)now.DayOfWeek - (int)targetDay;
            if (diff < 0) diff += 7;

            var startDate = now.AddDays(-diff);
            var endDate = startDate.AddDays(7).AddTicks(-1);

            return (startDate, endDate);
        }

        if (budget.Frequency == BudgetFrequency.Monthly)
        {
            if (!int.TryParse(budget.CommencementDay, out var dayOfMonth))
            {
                return Result.Failure<(DateTime startDate, DateTime endDate)>("Invalid commencement day for monthly budget");
            }

            // Determine if we are in the current month's cycle or the previous one
            var currentMonthCappedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(now.Year, now.Month));
            var currentMonthCandidate = new DateTime(now.Year, now.Month, currentMonthCappedDay);

            DateTime startDate;
            DateTime endDate;

            if (now >= currentMonthCandidate)
            {
                startDate = currentMonthCandidate;
                var nextMonth = startDate.AddMonths(1);
                var nextMonthCappedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                endDate = new DateTime(nextMonth.Year, nextMonth.Month, nextMonthCappedDay).AddTicks(-1);
            }
            else
            {
                var prevMonth = now.AddMonths(-1);
                var prevMonthCappedDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month));
                startDate = new DateTime(prevMonth.Year, prevMonth.Month, prevMonthCappedDay);
                endDate = currentMonthCandidate.AddTicks(-1);
            }

            return (startDate, endDate);
        }

        return Result.Failure<(DateTime startDate, DateTime endDate)>("Unsupported budget frequency");
    }

    public async Task<Result<decimal>> GetSpentAmount(Budget budget, CancellationToken ct)
    {
        var datesResult = CalculateDates(budget);
        if (datesResult.IsFailure)
        {
            return Result.Failure<decimal>(datesResult.Error);
        }

        var (startDate, endDate) = datesResult.Value;

        var groups = await _groupsRepository.GetAllByUserId(budget.UserId, ct);
        var membersByGroup = groups.ToDictionary(x => x.Id, x => x.Members.First(m => m.UserId == budget.UserId));
        var allMemberIds = membersByGroup.Values.Select(m => m.Id).ToList();

        var currencyRatesResult = await _currencyExchangeRateService.GetLatestStoredRates(ct);
        if (currencyRatesResult.IsFailure)
        {
            return currencyRatesResult.ConvertFailure<decimal>();
        }
        var rates = currencyRatesResult.Value;

        decimal totalSpent = 0;

        if (budget.Scope.HasFlag(BudgetScope.Group))
        {
            List<string> groupMemberIdsToConsider;
            if (budget.TargetGroupIds != null && budget.TargetGroupIds.Count != 0)
            {
                groupMemberIdsToConsider = budget.TargetGroupIds
                    .Where(membersByGroup.ContainsKey)
                    .Select(gid => membersByGroup[gid].Id)
                    .ToList();
            }
            else
            {
                groupMemberIdsToConsider = allMemberIds;
            }

            if (groupMemberIdsToConsider.Count != 0)
            {
                var groupExpenses = await _expensesRepository.GetGroupExpensesByMemberIds(groupMemberIdsToConsider, startDate, endDate, ct);
                var groupSpent = groupExpenses.Sum(x =>
                {
                    var shareAmount = x.Shares.FirstOrDefault(s => groupMemberIdsToConsider.Contains(s.MemberId))?.Amount ?? 0;
                    return _currencyExchangeRateService.Convert(shareAmount, x.Currency, rates, budget.Currency);
                });
                totalSpent += groupSpent;
            }
        }

        if (budget.Scope.HasFlag(BudgetScope.NonGroup))
        {
            var nonGroupExpenses = await _expensesRepository.GetNonGroupExpensesByUserId(budget.UserId, startDate, endDate, ct);
            var nonGroupSpent = nonGroupExpenses.Sum(x =>
            {
                var shareAmount = x.Shares.FirstOrDefault(s => s.UserId == budget.UserId)?.Amount ?? 0;
                return _currencyExchangeRateService.Convert(shareAmount, x.Currency, rates, budget.Currency);
            });
            totalSpent += nonGroupSpent;
        }

        if (budget.Scope.HasFlag(BudgetScope.Personal))
        {
            var personalExpenses = await _expensesRepository.GetPersonalExpensesByUserId(budget.UserId, allMemberIds, ct, startDate, endDate);
            var personalSpent = personalExpenses.OfType<PersonalExpense>().Sum(x => _currencyExchangeRateService.Convert(x.Amount, x.Currency, rates, budget.Currency));
            totalSpent += personalSpent;
        }

        return totalSpent;
    }
}