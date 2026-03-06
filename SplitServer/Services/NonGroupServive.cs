using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Extensions;

namespace SplitServer.Services;

public class NonGroupService
{

    public static List<NonGroupDebt> GetDebts(List<NonGroupExpense> expenses, List<NonGroupTransfer> transfers,
        string userId, IList<User>? users)
    {
        var currencies = expenses.Select(x => x.Currency).Concat(transfers.Select(x => x.Currency)).Distinct().ToList();

        return currencies.SelectMany(c => GetDebtsForCurrency(c, expenses, transfers, userId, users)).ToList();
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSpent(List<NonGroupExpense> expenses)
    {
        var totalSpentByUser = new Dictionary<string, Dictionary<string, decimal>>();
        var expensesByCurrency = expenses.GroupBy(x => x.Currency).ToList();
        var userIds = expenses.SelectMany(e => e.Shares.Select(s => s.UserId).Concat(e.Payments.Select(p => p.UserId)))
            .Distinct().ToHashSet();

        foreach (var id in userIds)
        {
            totalSpentByUser[id] = expensesByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .SelectMany(expense => expense.Shares)
                    .Where(share => share.UserId == id)
                    .Sum(share => share.Amount));
        }

        return totalSpentByUser;
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalReceived(List<NonGroupTransfer> transfers)
    {
        var totalReceivedByUser = new Dictionary<string, Dictionary<string, decimal>>();
        var transfersByCurrency = transfers.GroupBy(x => x.Currency).ToList();
        var receiversIds = transfers.Select(t => t.ReceiverId).Distinct();
        var sendersIds = transfers.Select(t => t.SenderId).Distinct();
        var userIds = receiversIds.Concat(sendersIds).Distinct().ToList();

        foreach (var id in userIds)
        {
            totalReceivedByUser[id] = transfersByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .Where(transfer => transfer.ReceiverId == id)
                    .Sum(transfer => transfer.Amount));
        }

        return totalReceivedByUser;
    }

    public static Dictionary<string, Dictionary<string, decimal>> GetTotalSent(List<NonGroupTransfer> transfers)
    {
        var totalSentByUser = new Dictionary<string, Dictionary<string, decimal>>();
        var transfersByCurrency = transfers.GroupBy(x => x.Currency).ToList();
        var receiversIds = transfers.Select(t => t.ReceiverId).Distinct();
        var sendersIds = transfers.Select(t => t.SenderId).Distinct();
        var userIds = receiversIds.Concat(sendersIds).Distinct().ToList();

        foreach (var id in userIds)
        {
            totalSentByUser[id] = transfersByCurrency.ToDictionary(
                currencyGroup => currencyGroup.Key,
                currencyGroup => currencyGroup
                    .Where(transfer => transfer.SenderId == id)
                    .Sum(transfer => transfer.Amount));
        }

        return totalSentByUser;
    }

    private static List<NonGroupDebt> GetDebtsForCurrency(string currency, List<NonGroupExpense> expenses,
        List<NonGroupTransfer> transfers, string userId, IList<User>? users)
    {
        var debts = new List<NonGroupDebt>();

        // Identify all other users involved in these transactions
        var expensesUserIds = expenses
            .Where(e => e.Currency == currency)
            .SelectMany(e => e.Shares.Select(s => s.UserId).Concat(e.Payments.Select(p => p.UserId)));

        var transfersUserIds = transfers
            .Where(t => t.Currency == currency)
            .SelectMany(t => new[] { t.SenderId, t.ReceiverId });

        var otherUserIds = expensesUserIds.Concat(transfersUserIds)
            .Distinct()
            .Where(id => id != userId)
            .ToList();

        foreach (var pairedUserId in otherUserIds)
        {
            // Filter expenses where BOTH userId and pairedUserId are participants
            var pairExpenses = expenses
                .Where(e => e.Currency == currency && IsInExpense(e, userId) && IsInExpense(e, pairedUserId))
                .ToList();

            // Filter transfers between userId and pairedUserId
            var pairTransfers = transfers
                .Where(t => t.Currency == currency &&
                            ((t.SenderId == userId && t.ReceiverId == pairedUserId) ||
                             (t.SenderId == pairedUserId && t.ReceiverId == userId)))
                .ToList();

            if (!pairExpenses.Any() && !pairTransfers.Any())
            {
                continue;
            }

            // Calculate debts for this specific subset of transactions
            // This treats the pair (and any unavoidable third parties in those shared expenses) as a system,
            // but we only extract the debt relevant to the pair.
            var subsetDebts = CalculateDebtsForSubset(pairExpenses, pairTransfers, currency, users);

            var pairDebt = subsetDebts.FirstOrDefault(d =>
                (d.Debtor == userId && d.Creditor == pairedUserId) ||
                (d.Debtor == pairedUserId && d.Creditor == userId));

            if (pairDebt != null)
            {
                debts.Add(pairDebt);
            }
        }

        return debts;
    }

    private static bool IsInExpense(NonGroupExpense expense, string userId)
    {
        return expense.Shares.Any(s => s.UserId == userId) || expense.Payments.Any(p => p.UserId == userId);
    }

    private static List<NonGroupDebt> CalculateDebtsForSubset(List<NonGroupExpense> expenses,
        List<NonGroupTransfer> transfers, string currency,  IList<User>? users)
    {
        var balances = new Dictionary<string, decimal>();

        foreach (var expense in expenses)
        {
            foreach (var share in expense.Shares)
            {
                balances[share.UserId] = balances.GetValueOrDefault(share.UserId) + share.Amount;
            }

            foreach (var payment in expense.Payments)
            {
                balances[payment.UserId] = balances.GetValueOrDefault(payment.UserId) - payment.Amount;
            }
        }

        foreach (var transfer in transfers)
        {
            balances[transfer.ReceiverId] = balances.GetValueOrDefault(transfer.ReceiverId) + transfer.Amount;
            balances[transfer.SenderId] = balances.GetValueOrDefault(transfer.SenderId) - transfer.Amount;
        }

        balances = balances.Where(x => x.Value != 0).ToDictionary(x => x.Key, x => x.Value);

        var debts = new List<NonGroupDebt>();

        while (balances.Any(x => x.Value != 0))
        {
            var maxDebtor = balances.MaxBy(x => x.Value);
            var maxCreditor = balances.MinBy(x => x.Value);

            // This should not happen in a zero-sum system, but handling float precision issues or empty states
            if (maxDebtor.Key == null || maxCreditor.Key == null) break;

            var amount = Math.Min(maxDebtor.Value, -maxCreditor.Value);

            // Round to prevent infinite loops with tiny precision errors? 
            // Standard decimal should be fine, but good to be safe if amounts are 0.
            if (amount == 0) break;

            var debtorName = users?.FirstOrDefault(u => u.Id == maxDebtor.Key)?.Username 
                             ?? DeletedUser.Username(maxDebtor.Key);
                             
            var creditorName = users?.FirstOrDefault(u => u.Id == maxCreditor.Key)?.Username 
                               ?? DeletedUser.Username(maxCreditor.Key);

            var debt = new NonGroupDebt
            {
                Debtor = maxDebtor.Key,
                DebtorName = debtorName,
                Creditor = maxCreditor.Key,
                CreditorName = creditorName,
                Amount = amount,
                Currency = currency
            };

            debts.Add(debt);

            balances[maxDebtor.Key] -= amount;
            balances[maxCreditor.Key] += amount;

            // Cleanup zero balances to keep 'Any' check accurate
            if (balances[maxDebtor.Key] == 0) balances.Remove(maxDebtor.Key);
            if (balances[maxCreditor.Key] == 0) balances.Remove(maxCreditor.Key);
        }

        return debts;
    }

    public static List<NonGroupExpense> CalculateFilteredExpensesList(GetNonGroupDebtsQuery query, List<NonGroupExpense> expenses, string userTimeZoneId)
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
            filteredExpenses = filteredExpenses.Where(x => x.Shares.Any(s => query.ParticipantIds.Contains(s.UserId)));
        }

        if (query.PayerIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Payments.Any(p => query.PayerIds.Contains(p.UserId)));
        }

        var labelIds = query.LabelIds?.Select(id => id.Contains('_') ? id.Split('_')[1] : id).ToArray();
        
        if (labelIds is { Length: > 0 })
        {
            filteredExpenses = filteredExpenses.Where(x => x.Labels.Any(l => labelIds.Contains(l)));
        }

        return filteredExpenses.ToList();
    }

    public static List<NonGroupTransfer> CalculateFilteredTransfersList(GetNonGroupDebtsQuery query, List<NonGroupTransfer> transfers, string userTimeZoneId)
    {
        var filteredTransfers = transfers.AsEnumerable();

        if (query.After.HasValue)
        {
            var afterUtc = query.After.Value.ToUtc(userTimeZoneId);
            filteredTransfers = filteredTransfers.Where(x => x.Occurred >= afterUtc);
        }

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