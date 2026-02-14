using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
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

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var groupExpenses = await _expensesRepository.GetAllByGroupId(group.Id, ct);
        var groupTransfers = await _transfersRepository.GetAllByGroupId(group.Id, ct);

        var filteredExpenses = groupExpenses.AsEnumerable();
        var filteredTransfers = groupTransfers.AsEnumerable();

        if (query.After.HasValue)
        {
            var afterUtc = query.After.Value.ToUtc(userTimeZoneId);
            filteredExpenses = filteredExpenses.Where(x => x.Occurred >= afterUtc);
            filteredTransfers = filteredTransfers.Where(x => x.Occurred >= afterUtc);
        }

        if (query.Before.HasValue)
        {
            var beforeUtc = query.Before.Value.ToUtc(userTimeZoneId);
            filteredExpenses = filteredExpenses.Where(x => x.Occurred <= beforeUtc);
            filteredTransfers = filteredTransfers.Where(x => x.Occurred <= beforeUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            filteredExpenses = filteredExpenses.Where(x => x.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase));
            filteredTransfers = filteredTransfers.Where(x => x.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ParticipantIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Shares.Any(s => query.ParticipantIds.Contains(s.MemberId)));
        }

        if (query.PayerIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Payments.Any(p => query.PayerIds.Contains(p.MemberId)));
        }

        if (query.LabelIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Labels.Any(l => query.LabelIds.Contains(l)));
        }

        if (query.ReceiverIds is { Length: > 0 })
        {
            filteredTransfers = filteredTransfers.Where(x => query.ReceiverIds.Contains(x.ReceiverId));
        }

        if (query.SenderIds is { Length: > 0 })
        {
            filteredTransfers = filteredTransfers.Where(x => query.SenderIds.Contains(x.SenderId));
        }

        var filteredExpensesList = filteredExpenses.ToList();
        var filteredTransfersList = filteredTransfers.ToList();

        var totalSpentByMember = GroupService.GetTotalSpent(group, filteredExpensesList);
        
        return new GetGroupDebtsResponse
        {
            Debts = GroupService.GetDebts(groupExpenses, groupTransfers),
            TotalSpent = totalSpentByMember,
            ConvertedTotalSpent = await GetConvertedTotalSpent(query.UserId, totalSpentByMember, ct),
            TotalSent = GroupService.GetTotalSent(group, filteredTransfersList),
            TotalReceived = GroupService.GetTotalReceived(group, filteredTransfersList),
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