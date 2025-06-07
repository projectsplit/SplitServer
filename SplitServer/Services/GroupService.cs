using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Requests;

namespace SplitServer.Services;

public class GroupService
{
    private readonly IGroupsRepository _groupsRepository;

    public GroupService(IGroupsRepository groupsRepository)
    {
        _groupsRepository = groupsRepository;
    }

    public static List<Debt> GetDebts(Group group, List<Expense> expenses, List<Transfer> transfers)
    {
        var currencies = expenses.Select(x => x.Currency).Concat(transfers.Select(x => x.Currency)).Distinct().ToList();

        return currencies.SelectMany(c => GetDebtsForCurrency(c, expenses, transfers)).ToList();
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSpent(Group group, List<Expense> expenses)
    {
        var totalSpentByMember = new Dictionary<string, Dictionary<string, decimal>>();
        var expensesByCurrency = expenses.GroupBy(x => x.Currency).ToList();

        foreach (var memberId in group.Members.Select(m => m.Id).Concat(group.Guests.Select(g => g.Id)))
        {
            totalSpentByMember[memberId] = expensesByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .SelectMany(expense => expense.Shares)
                    .Where(share => share.MemberId == memberId)
                    .Sum(share => share.Amount));
        }

        return totalSpentByMember;
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalReceived(Group group, List<Transfer> transfers)
    {
        var totalReceivedByMember = new Dictionary<string, Dictionary<string, decimal>>();
        var transfersByCurrency = transfers.GroupBy(x => x.Currency).ToList();

        foreach (var memberId in group.Members.Select(m => m.Id).Concat(group.Guests.Select(g => g.Id)))
        {
            totalReceivedByMember[memberId] = transfersByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .Where(transfer => transfer.ReceiverId == memberId)
                    .Sum(transfer => transfer.Amount));
        }

        return totalReceivedByMember;
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSent(Group group, List<Transfer> transfers)
    {
        var totalSentByMember = new Dictionary<string, Dictionary<string, decimal>>();
        var transfersByCurrency = transfers.GroupBy(x => x.Currency).ToList();

        foreach (var memberId in group.Members.Select(m => m.Id).Concat(group.Guests.Select(g => g.Id)))
        {
            totalSentByMember[memberId] = transfersByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .Where(transfer => transfer.SenderId == memberId)
                    .Sum(transfer => transfer.Amount));
        }

        return totalSentByMember;
    }

    private static List<Debt> GetDebtsForCurrency(string currency, List<Expense> expenses, List<Transfer> transfers)
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
            var maxDebtor = balances.MaxBy(x => x.Value);
            var maxCreditor = balances.MinBy(x => x.Value);

            var amount = Math.Min(maxDebtor.Value, -maxCreditor.Value);

            var debt = new Debt
            {
                Debtor = maxDebtor.Key,
                Creditor = maxCreditor.Key,
                Amount = amount,
                Currency = currency
            };

            debts.Add(debt);

            balances[maxDebtor.Key] -= amount;
            balances[maxCreditor.Key] += amount;
        }

        return debts;
    }

    public static List<Label> CreateLabelsWithIds(List<LabelRequestItem> labelItems, List<Label> groupLabels)
    {
        return labelItems
            .Select(
                x =>
                    groupLabels.SingleOrDefault(xx => xx.Text == x.Text) ??
                    new Label { Id = Guid.NewGuid().ToString(), Text = x.Text, Color = x.Color })
            .ToList();
    }

    public async Task<Result> AddLabelsToGroupIfMissing(Group group, List<Label> labels, DateTime now, CancellationToken ct)
    {
        var labelsNotInGroup = labels.Where(x => !group.Labels.Select(xx => xx.Id).Contains(x.Id)).ToList();

        if (labelsNotInGroup.Count <= 0)
        {
            return Result.Success();
        }

        return await _groupsRepository.Update(
            group with { Labels = group.Labels.Concat(labelsNotInGroup).DistinctBy(x => x.Id).ToList(), Updated = now },
            ct);
    }
}