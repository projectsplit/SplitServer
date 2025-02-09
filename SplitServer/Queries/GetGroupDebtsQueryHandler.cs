using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetGroupDebtsQueryHandler : IRequestHandler<GetGroupDebtsQuery, Result<GetGroupDebtsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetGroupDebtsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
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

        var expenses = await _expensesRepository.GetAllByGroupId(query.GroupId, ct);
        var transfers = await _transfersRepository.GetAllByGroupId(query.GroupId, ct);

        var currencies = expenses.Select(x => x.Currency).Concat(transfers.Select(x => x.Currency)).Distinct().ToList();

        return new GetGroupDebtsResponse
        {
            Debts = currencies.SelectMany(c => GetDebts(c, expenses, transfers)).ToList()
        };
    }

    private static List<Debt> GetDebts(string currency, List<Expense> expenses, List<Transfer> transfers)
    {
        var balances = new Dictionary<string, decimal>();

        foreach (var expense in expenses.Where(e => e.Currency == currency).ToList())
        {
            foreach (var share in expense.Shares)
            {
                balances[share.MemberId] = balances.GetValueOrDefault(share.MemberId) + share.Amount;
            }

            foreach (var payment in expense.Payments)
            {
                balances[payment.MemberId] = balances.GetValueOrDefault(payment.MemberId) - payment.Amount;
            }
        }

        foreach (var transfer in transfers.Where(t => t.Currency == currency).ToList())
        {
            balances[transfer.ReceiverId] = balances.GetValueOrDefault(transfer.ReceiverId) + transfer.Amount;
            balances[transfer.SenderId] = balances.GetValueOrDefault(transfer.SenderId) - transfer.Amount;
        }

        balances = balances.Where(x => x.Value != 0).ToDictionary(x => x.Key, x => x.Value);

        var debts = new List<Debt>();

        while (balances.Any(x => x.Value != 0))
        {
            var maxDebtor = balances.MinBy(x => x.Value);
            var maxCreditor = balances.MaxBy(x => x.Value);

            var amount = Math.Min(-maxDebtor.Value, maxCreditor.Value);

            var debt = new Debt
            {
                Debtor = maxDebtor.Key,
                Creditor = maxCreditor.Key,
                Amount = amount,
                Currency = currency
            };

            debts.Add(debt);

            balances[maxDebtor.Key] += amount;
            balances[maxCreditor.Key] -= amount;
        }

        return debts;
    }
}