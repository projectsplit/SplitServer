using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetAllGroupsTotalBalancesQueryHandler :
    IRequestHandler<GetAllGroupsTotalBalancesQuery, Result<GetAllGroupsTotalBalancesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public GetAllGroupsTotalBalancesQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        CurrencyExchangeRateService currencyExchangeRateService,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result<GetAllGroupsTotalBalancesResponse>> Handle(GetAllGroupsTotalBalancesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetAllGroupsTotalBalancesResponse>($"User with id {query.UserId} was not found");
        }

        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var membersByGroup = groups.ToDictionary(x => x.Id, x => x.Members.First(m => m.UserId == query.UserId));
        var memberIds = membersByGroup.Select(m => m.Value.Id).ToList();

        var expenses = await _expensesRepository.GetAllByMemberIds(memberIds, ct);
        var transfers = await _transfersRepository.GetAllByMemberIds(memberIds, ct);

        var balancesByCurrency = new Dictionary<string, decimal>();

        foreach (var expense in expenses)
        {
            var memberId = membersByGroup[expense.GroupId].Id;

            var shareAmount = expense.Shares.FirstOrDefault(x => x.MemberId == memberId)?.Amount ?? 0;
            balancesByCurrency[expense.Currency] = balancesByCurrency.GetValueOrDefault(expense.Currency) + shareAmount;

            var paymentAmount = expense.Payments.FirstOrDefault(x => x.MemberId == memberId)?.Amount ?? 0;
            balancesByCurrency[expense.Currency] = balancesByCurrency.GetValueOrDefault(expense.Currency) - paymentAmount;
        }

        foreach (var transfer in transfers)
        {
            var memberId = membersByGroup[transfer.GroupId].Id;

            if (transfer.SenderId == memberId)
            {
                balancesByCurrency[transfer.Currency] = balancesByCurrency.GetValueOrDefault(transfer.Currency) - transfer.Amount;
            }

            if (transfer.ReceiverId == memberId)
            {
                balancesByCurrency[transfer.Currency] = balancesByCurrency.GetValueOrDefault(transfer.Currency) + transfer.Amount;
            }
        }

        return new GetAllGroupsTotalBalancesResponse
        {
            Balances = balancesByCurrency,
            GroupCount = groups.Count,
            ConvertedBalance = await GetConvertedBalance(query.UserId, balancesByCurrency, ct)
        };
    }

    private async Task<decimal> GetConvertedBalance(
        string userId,
        Dictionary<string, decimal> balanceByCurrency,
        CancellationToken ct)
    {
        var ratesResult = await _currencyExchangeRateService.GetLatestStoredRates(ct);
        var rates = ratesResult.GetValueOrDefault();

        var preferredCurrencyMaybe = await _userPreferencesRepository.GetById(userId, ct);
        var preferredCurrency = preferredCurrencyMaybe.GetValueOrDefault()?.Currency ?? DefaultValues.Currency;

        return balanceByCurrency
            .Select(x => _currencyExchangeRateService.Convert(x.Value, x.Key, rates, preferredCurrency))
            .Sum();
    }
}