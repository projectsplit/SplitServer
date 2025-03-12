using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services;

public class DebtService
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public DebtService(
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<List<Debt>> GetDebts(string groupId, CancellationToken ct)
    {
        var expenses = await _expensesRepository.GetAllByGroupId(groupId, ct);
        var transfers = await _transfersRepository.GetAllByGroupId(groupId, ct);

        var currencies = expenses.Select(x => x.Currency).Concat(transfers.Select(x => x.Currency)).Distinct().ToList();

        return currencies.SelectMany(c => GetDebtsForCurrency(c, expenses, transfers)).ToList();
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