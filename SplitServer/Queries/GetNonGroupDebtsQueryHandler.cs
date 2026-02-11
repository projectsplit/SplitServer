using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.CurrencyExchangeRate;
using SplitServer.Services;
using SplitServer.Models;

namespace SplitServer.Queries;

public class GetNonGroupDebtsQueryHandler: IRequestHandler<GetNonGroupDebtsQuery, Result<GetNonGroupDebtsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public GetNonGroupDebtsQueryHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        CurrencyExchangeRateService currencyExchangeRateService,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result<GetNonGroupDebtsResponse>> Handle(GetNonGroupDebtsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetNonGroupDebtsResponse>($"User with id {query.UserId} was not found");
        }
        var user = userMaybe.Value;
        
        var nonGroupExpenses = await _expensesRepository.GetAllByUserId(user.Id, ct);
        var nonGroupTransfers = await _transfersRepository.GetAllByUserId(user.Id, ct);

        var usersIds = GetUniqueUsersIds(nonGroupExpenses, nonGroupTransfers).ToList();
        var users = await _usersRepository.GetByIds(usersIds,ct);

        var totalSpentByMember = NonGroupService.GetTotalSpent(nonGroupExpenses);
        
        return new GetNonGroupDebtsResponse
        {
            Debts = NonGroupService.GetDebts(nonGroupExpenses, nonGroupTransfers, query.UserId, users),
            TotalSpent = totalSpentByMember,
            ConvertedTotalSpent = await GetConvertedTotalSpent(query.UserId, totalSpentByMember, ct),
            TotalSent = NonGroupService.GetTotalSent(nonGroupTransfers),
            TotalReceived = NonGroupService.GetTotalReceived(nonGroupTransfers),
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

    private static IEnumerable<string> GetUniqueUsersIds (List<NonGroupExpense> expenses, List<NonGroupTransfer> transfers){
       
        var expensesUserIds = expenses
         .SelectMany(e => e.Shares.Select(s => s.UserId).Concat(e.Payments.Select(p => p.UserId)));
            
        var transfersUserIds = transfers
         .SelectMany(t => new[] { t.SenderId, t.ReceiverId });

        return expensesUserIds.Concat(transfersUserIds).Distinct();
    }
}