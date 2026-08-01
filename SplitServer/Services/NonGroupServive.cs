using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Extensions;

namespace SplitServer.Services;

public class NonGroupService
{
    /// <summary>
    /// Non group debts are strictly pairwise. Each expense is settled on its own, among its own
    /// participants only, and the resulting obligations are accumulated into a per counterparty
    /// ledger. Debts are never simplified across expenses, so two users who never shared an expense
    /// or a transfer can never end up owing each other, even when a common counterparty links them.
    /// </summary>
    public static List<NonGroupDebt> GetDebts(
        List<NonGroupExpense> expenses,
        List<NonGroupTransfer> transfers,
        string userId,
        IList<User>? users)
    {
        // Positive balance means userId owes the counterparty, negative means the counterparty owes userId.
        var balances = new Dictionary<(string Currency, string CounterpartyId), decimal>();

        foreach (var expense in expenses)
        {
            foreach (var (debtor, creditor, amount) in SettleExpense(expense))
            {
                if (debtor == userId)
                {
                    AddToBalance(balances, expense.Currency, creditor, amount);
                }
                else if (creditor == userId)
                {
                    AddToBalance(balances, expense.Currency, debtor, -amount);
                }
            }
        }

        foreach (var transfer in transfers)
        {
            if (transfer.SenderId == userId && transfer.ReceiverId != userId)
            {
                AddToBalance(balances, transfer.Currency, transfer.ReceiverId, -transfer.Amount);
            }
            else if (transfer.ReceiverId == userId && transfer.SenderId != userId)
            {
                AddToBalance(balances, transfer.Currency, transfer.SenderId, transfer.Amount);
            }
        }

        return balances
            .Where(x => x.Value != 0)
            .Select(x => CreateDebt(userId, x.Key.CounterpartyId, x.Key.Currency, x.Value, users))
            .OrderBy(x => x.Currency, StringComparer.Ordinal)
            .ThenBy(x => x.Debtor == userId ? x.CreditorName : x.DebtorName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddToBalance(
        Dictionary<(string Currency, string CounterpartyId), decimal> balances,
        string currency,
        string counterpartyId,
        decimal amount)
    {
        var key = (currency, counterpartyId);
        balances[key] = balances.GetValueOrDefault(key) + amount;
    }

    private static NonGroupDebt CreateDebt(
        string userId,
        string counterpartyId,
        string currency,
        decimal balance,
        IList<User>? users)
    {
        var userOwesCounterparty = balance > 0;

        var debtorId = userOwesCounterparty ? userId : counterpartyId;
        var creditorId = userOwesCounterparty ? counterpartyId : userId;

        return new NonGroupDebt
        {
            Debtor = debtorId,
            DebtorName = GetUsername(debtorId, users),
            Creditor = creditorId,
            CreditorName = GetUsername(creditorId, users),
            Amount = Math.Abs(balance),
            Currency = currency
        };
    }

    private static string GetUsername(string userId, IList<User>? users)
    {
        return users?.FirstOrDefault(u => u.Id == userId)?.Username ?? DeletedUser.Username(userId);
    }

    /// <summary>
    /// Settles a single expense in closed form: every participant's net position within that expense
    /// (share minus payment) is matched against the others, largest debtor against largest creditor.
    /// Because a validated expense has its shares and payments both summing to its amount, the net
    /// positions sum to zero and the matching is exact, so no rounding is ever introduced and every
    /// resulting amount stays a valid amount for the expense currency.
    /// </summary>
    private static IEnumerable<(string Debtor, string Creditor, decimal Amount)> SettleExpense(NonGroupExpense expense)
    {
        var balances = new Dictionary<string, decimal>();

        foreach (var share in expense.Shares)
        {
            balances[share.UserId] = balances.GetValueOrDefault(share.UserId) + share.Amount;
        }

        foreach (var payment in expense.Payments)
        {
            balances[payment.UserId] = balances.GetValueOrDefault(payment.UserId) - payment.Amount;
        }

        // Ordering is explicit so that the settlement of an expense is identical for every viewer
        // and does not depend on the order the shares and payments happen to be stored in.
        var debtors = balances
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var creditors = balances
            .Where(x => x.Value < 0)
            .OrderBy(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var debtorIndex = 0;
        var creditorIndex = 0;
        var debtorRemaining = debtors.Select(x => x.Value).ToArray();
        var creditorRemaining = creditors.Select(x => -x.Value).ToArray();

        while (debtorIndex < debtors.Count && creditorIndex < creditors.Count)
        {
            var amount = Math.Min(debtorRemaining[debtorIndex], creditorRemaining[creditorIndex]);

            yield return (debtors[debtorIndex].Key, creditors[creditorIndex].Key, amount);

            debtorRemaining[debtorIndex] -= amount;
            creditorRemaining[creditorIndex] -= amount;

            if (debtorRemaining[debtorIndex] == 0)
            {
                debtorIndex++;
            }

            if (creditorRemaining[creditorIndex] == 0)
            {
                creditorIndex++;
            }
        }
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

    public static List<NonGroupExpense> CalculateFilteredExpensesList(
        GetNonGroupDebtsQuery query,
        List<NonGroupExpense> expenses,
        string userTimeZoneId)
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

    public static List<NonGroupTransfer> CalculateFilteredTransfersList(
        GetNonGroupDebtsQuery query,
        List<NonGroupTransfer> transfers,
        string userTimeZoneId)
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