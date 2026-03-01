using CSharpFunctionalExtensions;
using MediatR;
using SplitServer;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.CurrencyExchangeRate;

public class GetUserTotalsQueryHandler: IRequestHandler<GetUserTotalsQuery, Result<GetUserTotalsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IGroupsRepository _groupsRepository;
    

    public GetUserTotalsQueryHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository,
        CurrencyExchangeRateService currencyExchangeRateService,
        IUserPreferencesRepository userPreferencesRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
        _userPreferencesRepository = userPreferencesRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetUserTotalsResponse>> Handle(GetUserTotalsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUserTotalsResponse>($"User with id {query.UserId} was not found");
        }
        
        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;
        
        var userGroups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var memberIds = userGroups.SelectMany(g => g.Members.Where(m => m.UserId == query.UserId).Select(m => m.Id)).ToList();

        var expenses = await _expensesRepository.GetAllPersonalByUserId(query.UserId,memberIds, ct);

        var filteredExpensesList = CalculateFilteredExpensesList(query, expenses, userTimeZoneId);
        
        var totalSpentByCurrency = GetTotalSpent(filteredExpensesList, query.UserId, memberIds);
        
        return new GetUserTotalsResponse
        {
            TotalSpent = totalSpentByCurrency,
            ConvertedTotalSpent = await GetConvertedTotalSpent(query.UserId, totalSpentByCurrency, ct),
        };
    }

    private async Task<Dictionary<string, decimal>> GetConvertedTotalSpent(
        string userId,
        Dictionary<string, Dictionary<string, decimal>> totalSpentByMember,
        CancellationToken ct)
    {
        var ratesResult = await _currencyExchangeRateService.GetLatestStoredRates(ct);
        var rates = ratesResult.GetValueOrDefault();

        var preferredCurrencyMaybe = await _userPreferencesRepository.GetById(userId, ct);
        var preferredCurrency = preferredCurrencyMaybe.GetValueOrDefault()?.Currency ?? DefaultValues.Currency;

        return totalSpentByMember.ToDictionary(
            memberPair => memberPair.Key,
            memberPair => memberPair.Value
                .Select(
                    currencyPair => _currencyExchangeRateService.Convert(
                        currencyPair.Value,
                        currencyPair.Key,
                        rates,
                        preferredCurrency))
                .Sum());
    }

    private static  List<Expense> CalculateFilteredExpensesList (GetUserTotalsQuery query, List<Expense> expenses, string userTimeZoneId){
       
        var filteredExpenses = expenses.AsEnumerable();

        if (query.After.HasValue)
        {
            var afterUtc = query.After.Value.ToUtc(userTimeZoneId);
            filteredExpenses = filteredExpenses.Where(x => x.Occurred >= afterUtc);
        }

        if (query.Before.HasValue)
        {
            var beforeUtc = query.Before.Value.ToUtc(userTimeZoneId);
            filteredExpenses = filteredExpenses.Where(x => x.Occurred <= beforeUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            filteredExpenses = filteredExpenses.Where(x => x.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (query.LabelIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Labels.Any(l => query.LabelIds.Contains(l)));
        }

        return filteredExpenses.ToList();
    }

    private static Dictionary<string, Dictionary<string, decimal>> GetTotalSpent(
        List<Expense> expenses,
        string userId,
        List<string> memberIds)
    {
        var userTotals = expenses
            .Select(e => new { e.Currency, Amount = GetUserShareAmount(e, userId, memberIds) })
            .Where(x => x.Amount > 0)
            .GroupBy(x => x.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return new Dictionary<string, Dictionary<string, decimal>>
        {
            { userId, userTotals }
        };
    }

    private static decimal GetUserShareAmount(Expense e, string userId, List<string> memberIds)
    {
        return e switch
        {
            PersonalExpense pe => pe.Amount,
            NonGroupExpense nge => nge.Shares.FirstOrDefault(s => s.UserId == userId)?.Amount ?? 0,
            GroupExpense ge => ge.Shares.FirstOrDefault(s => memberIds.Contains(s.MemberId))?.Amount ?? 0,
            _ => 0
        };
    }
}