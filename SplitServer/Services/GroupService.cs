using SplitServer.Models;

namespace SplitServer.Services;

public class GroupService
{
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
}