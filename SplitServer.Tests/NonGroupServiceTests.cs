using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

public class NonGroupServiceTests
{
    private const string Currency = "EUR";

    private static NonGroupExpense Expense(
        (string UserId, decimal Amount)[] payments,
        (string UserId, decimal Amount)[] shares,
        string currency = Currency)
    {
        return new NonGroupExpense
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Occurred = DateTime.UtcNow,
            CreatorId = payments[0].UserId,
            Amount = shares.Sum(x => x.Amount),
            Description = "test",
            Currency = currency,
            Location = null,
            Labels = [],
            Payments = payments.Select(x => new Payment { UserId = x.UserId, Amount = x.Amount }).ToList(),
            Shares = shares.Select(x => new Share { UserId = x.UserId, Amount = x.Amount }).ToList()
        };
    }

    private static NonGroupTransfer Transfer(string senderId, string receiverId, decimal amount, string currency = Currency)
    {
        return new NonGroupTransfer
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Occurred = DateTime.UtcNow,
            CreatorId = senderId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Amount = amount,
            Currency = currency,
            Description = "test"
        };
    }

    /// <summary>
    /// Only the expenses a user takes part in are ever loaded for that user, so tests mirror that.
    /// </summary>
    private static List<NonGroupExpense> VisibleTo(string userId, List<NonGroupExpense> expenses)
    {
        return expenses
            .Where(e => e.Shares.Any(s => s.UserId == userId) || e.Payments.Any(p => p.UserId == userId))
            .ToList();
    }

    private static List<NonGroupTransfer> VisibleTo(string userId, List<NonGroupTransfer> transfers)
    {
        return transfers.Where(t => t.SenderId == userId || t.ReceiverId == userId).ToList();
    }

    /// <summary>Positive means userId owes, negative means userId is owed.</summary>
    private static decimal NetPosition(string userId, List<NonGroupExpense> expenses, List<NonGroupTransfer> transfers)
    {
        var fromExpenses = expenses.Sum(e =>
            (e.Shares.FirstOrDefault(s => s.UserId == userId)?.Amount ?? 0) -
            (e.Payments.FirstOrDefault(p => p.UserId == userId)?.Amount ?? 0));

        var fromTransfers = transfers.Sum(t =>
            (t.ReceiverId == userId ? t.Amount : 0) - (t.SenderId == userId ? t.Amount : 0));

        return fromExpenses + fromTransfers;
    }

    private static decimal SignedTotal(string userId, List<NonGroupDebt> debts)
    {
        return debts.Sum(d => d.Debtor == userId ? d.Amount : -d.Amount);
    }

    private static decimal AmountBetween(string userId, string counterpartyId, List<NonGroupDebt> debts)
    {
        var debt = debts.SingleOrDefault(d =>
            (d.Debtor == userId && d.Creditor == counterpartyId) ||
            (d.Debtor == counterpartyId && d.Creditor == userId));

        if (debt is null)
        {
            return 0;
        }

        return debt.Debtor == userId ? debt.Amount : -debt.Amount;
    }

    [Fact]
    public void GetDebts_ShouldSplitEvenly_WhenOnePersonPaysForTwo()
    {
        List<NonGroupExpense> expenses = [Expense([("A", 100)], [("A", 50), ("B", 50)])];

        var debts = NonGroupService.GetDebts(expenses, [], "A", null);

        var debt = Assert.Single(debts);
        Assert.Equal("B", debt.Debtor);
        Assert.Equal("A", debt.Creditor);
        Assert.Equal(50, debt.Amount);
        Assert.Equal(Currency, debt.Currency);
    }

    [Fact]
    public void GetDebts_ShouldOweEachPayerSeparately_WhenThreePeopleShareOneExpense()
    {
        List<NonGroupExpense> expenses = [Expense([("A", 90)], [("A", 30), ("B", 30), ("C", 30)])];

        var debts = NonGroupService.GetDebts(expenses, [], "A", null);

        Assert.Equal(2, debts.Count);
        Assert.Equal(-30, AmountBetween("A", "B", debts));
        Assert.Equal(-30, AmountBetween("A", "C", debts));
    }

    [Fact]
    public void GetDebts_ShouldNettOffOppositeDebts_WhenTwoPeopleShareTwoExpensesWithAThirdParty()
    {
        // A paid 100 covering B and C, then B paid 60 covering A and C.
        // B owes A 50 from the first and A owes B 30 from the second, so B owes A 20 overall.
        List<NonGroupExpense> expenses =
        [
            Expense([("A", 100)], [("B", 50), ("C", 50)]),
            Expense([("B", 60)], [("A", 30), ("C", 30)])
        ];

        var debts = NonGroupService.GetDebts(VisibleTo("A", expenses), [], "A", null);

        Assert.Equal(-20, AmountBetween("A", "B", debts));
        Assert.Equal(-50, AmountBetween("A", "C", debts));
    }

    [Fact]
    public void GetDebts_ShouldNotSimplifyAcrossStrangers_WhenAAndCNeverShareATransaction()
    {
        // A and B share an expense, B and C share another. A and C have never met,
        // so neither may ever see a debt against the other.
        List<NonGroupExpense> expenses =
        [
            Expense([("A", 100)], [("A", 50), ("B", 50)]),
            Expense([("B", 80)], [("B", 40), ("C", 40)])
        ];

        var debtsOfA = NonGroupService.GetDebts(VisibleTo("A", expenses), [], "A", null);
        var debtsOfC = NonGroupService.GetDebts(VisibleTo("C", expenses), [], "C", null);

        Assert.Equal(-50, AmountBetween("A", "B", debtsOfA));
        Assert.Equal(0, AmountBetween("A", "C", debtsOfA));
        Assert.DoesNotContain(debtsOfA, d => d.Debtor == "C" || d.Creditor == "C");

        Assert.Equal(40, AmountBetween("C", "B", debtsOfC));
        Assert.DoesNotContain(debtsOfC, d => d.Debtor == "A" || d.Creditor == "A");
    }

    [Fact]
    public void GetDebts_ShouldOnlyPairPayersWithParticipants_WhenAnExpenseHasSeveralPayers()
    {
        // A and B both paid, C paid nothing. C is the only net debtor, so C must be the only debtor,
        // and A and B may never end up owing each other out of an expense they both funded.
        List<NonGroupExpense> expenses = [Expense([("A", 60), ("B", 40)], [("A", 40), ("B", 30), ("C", 30)])];

        var debtsOfA = NonGroupService.GetDebts(expenses, [], "A", null);
        var debtsOfB = NonGroupService.GetDebts(expenses, [], "B", null);
        var debtsOfC = NonGroupService.GetDebts(expenses, [], "C", null);

        Assert.Equal(0, AmountBetween("A", "B", debtsOfA));
        Assert.Equal(-20, SignedTotal("A", debtsOfA));
        Assert.Equal(-10, SignedTotal("B", debtsOfB));
        Assert.Equal(30, SignedTotal("C", debtsOfC));
        Assert.All(debtsOfC, d => Assert.Equal("C", d.Debtor));
    }

    [Fact]
    public void GetDebts_ShouldSettleDebt_WhenATransferCoversItExactly()
    {
        List<NonGroupExpense> expenses = [Expense([("A", 100)], [("A", 50), ("B", 50)])];
        List<NonGroupTransfer> transfers = [Transfer("B", "A", 50)];

        var debts = NonGroupService.GetDebts(expenses, transfers, "A", null);

        Assert.Empty(debts);
    }

    [Fact]
    public void GetDebts_ShouldReverseDebt_WhenATransferOverpaysIt()
    {
        List<NonGroupExpense> expenses = [Expense([("A", 100)], [("A", 50), ("B", 50)])];
        List<NonGroupTransfer> transfers = [Transfer("B", "A", 80)];

        var debts = NonGroupService.GetDebts(expenses, transfers, "A", null);

        var debt = Assert.Single(debts);
        Assert.Equal("A", debt.Debtor);
        Assert.Equal("B", debt.Creditor);
        Assert.Equal(30, debt.Amount);
    }

    [Fact]
    public void GetDebts_ShouldKeepCurrenciesApart_WhenDebtsRunInOppositeDirections()
    {
        List<NonGroupExpense> expenses =
        [
            Expense([("A", 100)], [("A", 50), ("B", 50)]),
            Expense([("B", 40)], [("A", 20), ("B", 20)], "USD")
        ];

        var debts = NonGroupService.GetDebts(expenses, [], "A", null);

        Assert.Equal(2, debts.Count);
        var euro = Assert.Single(debts, d => d.Currency == "EUR");
        var dollar = Assert.Single(debts, d => d.Currency == "USD");
        Assert.Equal("B", euro.Debtor);
        Assert.Equal(50, euro.Amount);
        Assert.Equal("A", dollar.Debtor);
        Assert.Equal(20, dollar.Amount);
    }

    [Fact]
    public void GetDebts_ShouldAgreeBetweenBothParties_WhenTheyShareExpensesWithOthers()
    {
        List<NonGroupExpense> expenses =
        [
            Expense([("D", 210)], [("A", 75), ("B", 80), ("C", 45), ("D", 10)]),
            Expense([("B", 112), ("D", 43)], [("A", 95), ("B", 15), ("D", 45)]),
            Expense([("A", 19), ("B", 32), ("C", 64)], [("A", 70), ("B", 15), ("C", 30)])
        ];

        string[] users = ["A", "B", "C", "D"];

        var debtsByUser = users.ToDictionary(
            u => u,
            u => NonGroupService.GetDebts(VisibleTo(u, expenses), [], u, null));

        foreach (var user in users)
        {
            foreach (var counterparty in users.Where(x => x != user))
            {
                Assert.Equal(
                    AmountBetween(user, counterparty, debtsByUser[user]),
                    -AmountBetween(counterparty, user, debtsByUser[counterparty]));
            }
        }
    }

    [Fact]
    public void GetDebts_ShouldSumToTheUsersNetPosition_ForEveryParticipant()
    {
        List<NonGroupExpense> expenses =
        [
            Expense([("D", 210)], [("A", 75), ("B", 80), ("C", 45), ("D", 10)]),
            Expense([("B", 112), ("D", 43)], [("A", 95), ("B", 15), ("D", 45)]),
            Expense([("A", 19), ("B", 32), ("C", 64)], [("A", 70), ("B", 15), ("C", 30)])
        ];

        List<NonGroupTransfer> transfers = [Transfer("A", "D", 60), Transfer("C", "D", 15)];

        foreach (var user in new[] { "A", "B", "C", "D" })
        {
            var visibleExpenses = VisibleTo(user, expenses);
            var visibleTransfers = VisibleTo(user, transfers);

            var debts = NonGroupService.GetDebts(visibleExpenses, visibleTransfers, user, null);

            Assert.Equal(NetPosition(user, visibleExpenses, visibleTransfers), SignedTotal(user, debts));
        }
    }

    [Fact]
    public void GetDebts_ShouldSumToTheUsersNetPosition_ForRandomlyGeneratedExpenses()
    {
        string[] users = ["A", "B", "C", "D", "E"];
        var random = new Random(20260731);

        for (var run = 0; run < 5000; run++)
        {
            var expenses = new List<NonGroupExpense>();

            for (var i = 0; i < random.Next(1, 5); i++)
            {
                var participants = users.Where(_ => random.Next(2) == 0).ToArray();

                if (participants.Length < 2)
                {
                    continue;
                }

                var shares = participants.Select(p => (UserId: p, Amount: (decimal)random.Next(1, 40) * 5)).ToArray();
                var total = shares.Sum(x => x.Amount);

                var payers = participants.Where(_ => random.Next(2) == 0).ToArray();

                if (payers.Length == 0)
                {
                    payers = [participants[random.Next(participants.Length)]];
                }

                var remaining = total;
                var payments = new List<(string UserId, decimal Amount)>();

                for (var p = 0; p < payers.Length; p++)
                {
                    var isLast = p == payers.Length - 1;
                    var maximum = remaining - (payers.Length - p - 1);
                    var amount = isLast ? remaining : random.Next(1, (int)Math.Max(1, maximum));
                    remaining -= amount;

                    if (amount > 0)
                    {
                        payments.Add((payers[p], amount));
                    }
                }

                expenses.Add(Expense(payments.ToArray(), shares));
            }

            foreach (var user in users)
            {
                var visible = VisibleTo(user, expenses);
                var debts = NonGroupService.GetDebts(visible, [], user, null);

                Assert.Equal(NetPosition(user, visible, []), SignedTotal(user, debts));
            }
        }
    }

    [Fact]
    public void GetDebts_ShouldNotProduceDebtsBetweenUsersWithNoSharedTransaction()
    {
        string[] users = ["A", "B", "C", "D", "E"];
        var random = new Random(9182736);

        for (var run = 0; run < 2000; run++)
        {
            var expenses = new List<NonGroupExpense>();

            for (var i = 0; i < random.Next(1, 4); i++)
            {
                var participants = users.Where(_ => random.Next(2) == 0).ToArray();

                if (participants.Length < 2)
                {
                    continue;
                }

                var shares = participants.Select(p => (UserId: p, Amount: (decimal)random.Next(1, 20) * 5)).ToArray();
                var payer = participants[random.Next(participants.Length)];
                expenses.Add(Expense([(payer, shares.Sum(x => x.Amount))], shares));
            }

            foreach (var user in users)
            {
                var visible = VisibleTo(user, expenses);
                var debts = NonGroupService.GetDebts(visible, [], user, null);

                foreach (var debt in debts)
                {
                    var counterparty = debt.Debtor == user ? debt.Creditor : debt.Debtor;

                    Assert.Contains(
                        visible,
                        e => e.Shares.Any(s => s.UserId == counterparty) || e.Payments.Any(p => p.UserId == counterparty));
                }
            }
        }
    }
}
