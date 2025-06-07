using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetGroupDebtsQueryHandler : IRequestHandler<GetGroupDebtsQuery, Result<GetGroupDebtsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public GetGroupDebtsQueryHandler(
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

    public async Task<Result<GetGroupDebtsResponse>> Handle(GetGroupDebtsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDebtsResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDebtsResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupDebtsResponse>("User must be a group member");
        }

        var groupExpenses = await _expensesRepository.GetAllByGroupId(group.Id, ct);
        var groupTransfers = await _transfersRepository.GetAllByGroupId(group.Id, ct);

        var totalSpentByMember = GroupService.GetTotalSpent(group, groupExpenses);

        return new GetGroupDebtsResponse
        {
            Debts = GroupService.GetDebts(group, groupExpenses, groupTransfers),
            TotalSpent = totalSpentByMember,
            ConvertedTotalSpent = await GetConvertedTotalSpent(query.UserId, totalSpentByMember, ct),
            TotalSent = GroupService.GetTotalSent(group, groupTransfers),
            TotalReceived = GroupService.GetTotalReceived(group, groupTransfers),
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
}