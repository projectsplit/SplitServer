using System.Text;
using System.Text.Json;
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
        var memberDebts = new Dictionary<string, decimal>();
        var memberCredits = new Dictionary<string, decimal>();

        foreach (var expense in expenses.Where(e => e.Currency == currency).ToList())
        {
            foreach (var share in expense.Shares)
            {
                memberDebts[share.MemberId] = memberDebts.GetValueOrDefault(share.MemberId) + share.Amount;
            }

            foreach (var payment in expense.Payments)
            {
                memberCredits[payment.MemberId] = memberCredits.GetValueOrDefault(payment.MemberId) + payment.Amount;
            }
        }

        foreach (var transfer in transfers.Where(t => t.Currency == currency).ToList())
        {
            memberDebts[transfer.ReceiverId] = memberDebts.GetValueOrDefault(transfer.ReceiverId) + transfer.Amount;
            memberCredits[transfer.SenderId] = memberCredits.GetValueOrDefault(transfer.SenderId) + transfer.Amount;
        }

        var debts = new List<Debt>();

        while (memberCredits.Sum(x => x.Value) > 0)
        {
            var maxDebtor = memberDebts.MaxBy(x => x.Value);
            var maxCreditor = memberCredits.MaxBy(x => x.Value);

            var debt = new Debt
            {
                Debtor = maxDebtor.Key,
                Creditor = maxCreditor.Key,
                Amount = Math.Min(maxDebtor.Value, maxCreditor.Value),
                Currency = currency
            };

            debts.Add(debt);
            memberDebts[debt.Debtor] -= debt.Amount;
            memberCredits[debt.Creditor] -= debt.Amount;
        }

        return debts;
    }
}