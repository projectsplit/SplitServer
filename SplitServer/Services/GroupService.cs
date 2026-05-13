using CSharpFunctionalExtensions;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries;
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

    public static List<Debt> GetDebts(List<GroupExpense> expenses, List<GroupTransfer> transfers)
    {
        var currencies = expenses.Select(x => x.Currency).Concat(transfers.Select(x => x.Currency)).Distinct().ToList();

        return currencies.SelectMany(c => GetDebtsForCurrency(c, expenses, transfers)).ToList();
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSpent(Group group, List<GroupExpense> expenses)
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

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalReceived(Group group, List<GroupTransfer> transfers)
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

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSent(Group group, List<GroupTransfer> transfers)
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

    private static List<Debt> GetDebtsForCurrency(string currency, List<GroupExpense> expenses, List<GroupTransfer> transfers)
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
            .Select(x =>
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

    public static List<GroupExpense> CalculateFilteredExpensesList(GetGroupDebtsQuery query, List<GroupExpense> expenses, string userTimeZoneId)
    {
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

        return filteredExpenses.ToList();
    }

    public static List<GroupTransfer> CalculateFilteredTransfersList(GetGroupDebtsQuery query, List<GroupTransfer> transfers, string userTimeZoneId)
    {
        var filteredTransfers = transfers.AsEnumerable();

        if (query.Before.HasValue)
        {
            var beforeUtc = query.Before.Value.ToUtc(userTimeZoneId);
            filteredTransfers = filteredTransfers.Where(x => x.Occurred <= beforeUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            filteredTransfers = filteredTransfers.Where(x => x.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ReceiverIds is { Length: > 0 })
        {
            filteredTransfers = filteredTransfers.Where(x => query.ReceiverIds.Contains(x.ReceiverId));
        }

        if (query.SenderIds is { Length: > 0 })
        {
            filteredTransfers = filteredTransfers.Where(x => query.SenderIds.Contains(x.SenderId));
        }

        return filteredTransfers.ToList();
    }
}