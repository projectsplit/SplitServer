using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

public class OrphanedNonGroupRecordsTests
{
    private const string Leaving = "leaving";

    private static NonGroupExpense Expense(string id, string[] payers, string[] sharers)
    {
        return new NonGroupExpense
        {
            Id = id,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Occurred = DateTime.UtcNow,
            CreatorId = payers[0],
            Amount = sharers.Length,
            Description = "test",
            Currency = "EUR",
            Location = null,
            Labels = [],
            Payments = payers.Select(x => new Payment { UserId = x, Amount = 1 }).ToList(),
            Shares = sharers.Select(x => new Share { UserId = x, Amount = 1 }).ToList()
        };
    }

    private static NonGroupTransfer Transfer(string id, string senderId, string receiverId)
    {
        return new NonGroupTransfer
        {
            Id = id,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Occurred = DateTime.UtcNow,
            CreatorId = senderId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Amount = 1,
            Currency = "EUR",
            Description = "test"
        };
    }

    [Fact]
    public void An_expense_someone_else_can_still_open_is_kept()
    {
        var expenses = new[] { Expense("shared", [Leaving], [Leaving, "friend"]) };

        Assert.Empty(OrphanedNonGroupRecords.ExpenseIds(expenses, Leaving, new HashSet<string> { "friend" }));
    }

    [Fact]
    public void An_expense_only_the_leaving_person_was_in_goes_with_them()
    {
        var expenses = new[] { Expense("alone", [Leaving], [Leaving]) };

        Assert.Equal(["alone"], OrphanedNonGroupRecords.ExpenseIds(expenses, Leaving, new HashSet<string>()));
    }

    [Fact]
    public void An_expense_whose_other_participants_have_all_gone_goes_too()
    {
        var expenses = new[] { Expense("abandoned", ["gone"], [Leaving, "gone"]) };

        Assert.Equal(["abandoned"], OrphanedNonGroupRecords.ExpenseIds(expenses, Leaving, new HashSet<string>()));
    }

    [Fact]
    public void One_remaining_participant_is_enough_to_keep_an_expense()
    {
        var expenses = new[] { Expense("mixed", ["gone"], [Leaving, "gone", "friend"]) };

        Assert.Empty(OrphanedNonGroupRecords.ExpenseIds(expenses, Leaving, new HashSet<string> { "friend" }));
    }

    [Fact]
    public void A_transfer_is_kept_only_while_the_other_side_still_has_an_account()
    {
        var transfers = new[]
        {
            Transfer("to-friend", Leaving, "friend"),
            Transfer("from-gone", "gone", Leaving),
        };

        Assert.Equal(
            ["from-gone"],
            OrphanedNonGroupRecords.TransferIds(transfers, Leaving, new HashSet<string> { "friend" }));
    }

    [Fact]
    public void Everyone_else_named_is_looked_up_once_each_and_never_the_leaving_person()
    {
        var expenses = new[] { Expense("a", [Leaving], [Leaving, "friend"]), Expense("b", ["friend"], [Leaving, "other"]) };
        var transfers = new[] { Transfer("t", Leaving, "friend") };

        var ids = OrphanedNonGroupRecords.OtherUserIds(expenses, transfers, Leaving);

        Assert.Equal(["friend", "other"], ids.Order());
    }
}
