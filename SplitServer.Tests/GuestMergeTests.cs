using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

public class GuestMergeTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private const string Retired = "member-nikos";
    private const string Kept = "guest-maria";

    private static Member Member(string id, string userId, int joinedDaysAgo) =>
        new() { Id = id, UserId = userId, Joined = Now.AddDays(-joinedDaysAgo) };

    private static Guest Guest(string id, int joinedDaysAgo) =>
        new() { Id = id, Name = id, Joined = Now.AddDays(-joinedDaysAgo) };

    private static Group Group(Member[] members, Guest[] guests) =>
        new()
        {
            Id = "group",
            Created = Now.AddDays(-100),
            Updated = Now.AddDays(-1),
            OwnerId = "owner",
            Name = "Flat",
            Currency = "EUR",
            IsArchived = false,
            Members = [.. members],
            Guests = [.. guests],
            Labels = [],
        };

    private static GroupExpense Expense(
        string creatorId,
        (string MemberId, decimal Amount)[] payments,
        (string MemberId, decimal Amount)[] shares,
        string currency = "EUR") =>
        new()
        {
            Id = "expense",
            Created = Now.AddDays(-2),
            Updated = Now.AddDays(-2),
            Occurred = Now.AddDays(-2),
            GroupId = "group",
            CreatorId = creatorId,
            Amount = payments.Sum(x => x.Amount),
            Description = "test",
            Currency = currency,
            Location = null,
            Labels = [],
            Payments = payments.Select(x => new GroupPayment { MemberId = x.MemberId, Amount = x.Amount }).ToList(),
            Shares = shares.Select(x => new GroupShare { MemberId = x.MemberId, Amount = x.Amount }).ToList(),
        };

    private static GroupTransfer Transfer(string creatorId, string senderId, string receiverId, decimal amount = 10) =>
        new()
        {
            Id = "transfer",
            Created = Now.AddDays(-2),
            Updated = Now.AddDays(-2),
            Occurred = Now.AddDays(-2),
            GroupId = "group",
            CreatorId = creatorId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Amount = amount,
            Currency = "EUR",
            Description = "test",
        };

    private static Invitation Invitation(string? replacedMemberId) =>
        new()
        {
            Id = "invitation",
            Created = Now.AddDays(-1),
            Updated = Now.AddDays(-1),
            SenderId = "owner",
            ReceiverId = "nikos",
            GroupId = "group",
            GuestId = Kept,
            ReplacedMemberId = replacedMemberId,
        };

    [Fact]
    public void Claiming_puts_the_member_under_the_guests_id_and_keeps_their_join_date()
    {
        var nikos = Member(Retired, "nikos", joinedDaysAgo: 3);
        var owner = Member("member-owner", "owner", joinedDaysAgo: 90);
        var maria = Guest(Kept, joinedDaysAgo: 30);
        var other = Guest("guest-other", joinedDaysAgo: 20);

        var merged = GuestMerge.Claim(Group([owner, nikos], [maria, other]), nikos, maria, Now);

        Assert.Equal(["member-owner", Kept], merged.Members.Select(x => x.Id));
        var member = merged.Members.Single(x => x.Id == Kept);
        Assert.Equal("nikos", member.UserId);
        Assert.Equal(nikos.Joined, member.Joined);
        Assert.Equal(["guest-other"], merged.Guests.Select(x => x.Id));
        Assert.Equal(Now, merged.Updated);
    }

    [Fact]
    public void Taking_over_turns_the_guest_into_a_member_under_the_same_id()
    {
        var owner = Member("member-owner", "owner", joinedDaysAgo: 90);
        var maria = Guest(Kept, joinedDaysAgo: 30);

        var taken = GuestMerge.TakeOver(Group([owner], [maria]), maria, "nikos", Now);

        var member = Assert.Single(taken.Members, x => x.UserId == "nikos");
        Assert.Equal(Kept, member.Id);
        Assert.Equal(Now, member.Joined);
        Assert.Empty(taken.Guests);
    }

    [Fact]
    public void A_merge_is_unfinished_only_once_the_group_write_has_landed()
    {
        var owner = Member("member-owner", "owner", joinedDaysAgo: 90);

        var beforeClaim = Group([owner, Member(Retired, "nikos", 3)], [Guest(Kept, 30)]);
        var afterClaim = Group([owner, Member(Kept, "nikos", 3)], []);

        Assert.False(GuestMerge.IsUnfinished(Invitation(replacedMemberId: null), beforeClaim));
        Assert.False(GuestMerge.IsUnfinished(Invitation(replacedMemberId: Retired), beforeClaim));
        Assert.False(GuestMerge.IsUnfinished(Invitation(replacedMemberId: null), afterClaim));
        Assert.True(GuestMerge.IsUnfinished(Invitation(replacedMemberId: Retired), afterClaim));
    }

    [Fact]
    public void Rewriting_an_expense_renames_the_retired_id_everywhere()
    {
        var expense = Expense(
            creatorId: Retired,
            payments: [(Retired, 30)],
            shares: [("member-owner", 10), (Retired, 20)]);

        var rewritten = GuestMerge.Rewrite(expense, Retired, Kept, Now);

        Assert.Equal(Kept, rewritten.CreatorId);
        Assert.Equal([(Kept, 30m)], rewritten.Payments.Select(x => (x.MemberId, x.Amount)));
        Assert.Equal([("member-owner", 10m), (Kept, 20m)], rewritten.Shares.Select(x => (x.MemberId, x.Amount)));
        Assert.Equal(30, rewritten.Amount);
        Assert.Equal(Now, rewritten.Updated);
    }

    [Fact]
    public void Lines_the_two_ids_both_had_become_one_carrying_the_sum()
    {
        var expense = Expense(
            creatorId: "member-owner",
            payments: [("member-owner", 20), (Retired, 5), (Kept, 5)],
            shares: [(Kept, 10), ("member-owner", 10), (Retired, 10)]);

        var rewritten = GuestMerge.Rewrite(expense, Retired, Kept, Now);

        Assert.Equal([("member-owner", 20m), (Kept, 10m)], rewritten.Payments.Select(x => (x.MemberId, x.Amount)));
        Assert.Equal([(Kept, 20m), ("member-owner", 10m)], rewritten.Shares.Select(x => (x.MemberId, x.Amount)));
        Assert.Equal(30, rewritten.Amount);
    }

    [Fact]
    public void An_expense_without_the_retired_id_comes_back_as_it_was()
    {
        var expense = Expense(
            creatorId: "member-owner",
            payments: [("member-owner", 10)],
            shares: [("member-owner", 5), (Kept, 5)]);

        var rewritten = GuestMerge.Rewrite(expense, Retired, Kept, Now);

        Assert.Equal(expense with { Updated = Now }, rewritten with { Payments = expense.Payments, Shares = expense.Shares });
        Assert.Equal(expense.Payments, rewritten.Payments);
        Assert.Equal(expense.Shares, rewritten.Shares);
    }

    [Fact]
    public void Rewriting_a_transfer_renames_either_end_and_the_creator()
    {
        var rewritten = GuestMerge.Rewrite(Transfer(creatorId: Retired, senderId: Retired, receiverId: "member-owner"), Retired, Kept, Now);

        Assert.NotNull(rewritten);
        Assert.Equal(Kept, rewritten.CreatorId);
        Assert.Equal(Kept, rewritten.SenderId);
        Assert.Equal("member-owner", rewritten.ReceiverId);
        Assert.Equal(Now, rewritten.Updated);
    }

    [Fact]
    public void A_transfer_between_the_two_ids_is_dropped()
    {
        Assert.Null(GuestMerge.Rewrite(Transfer("member-owner", senderId: Retired, receiverId: Kept), Retired, Kept, Now));
        Assert.Null(GuestMerge.Rewrite(Transfer("member-owner", senderId: Kept, receiverId: Retired), Retired, Kept, Now));
    }

    [Fact]
    public void A_guests_balance_is_what_they_owe_per_currency_after_transfers()
    {
        var expenses = new List<GroupExpense>
        {
            // Maria owes 20 of the owner's 30.
            Expense("member-owner", [("member-owner", 30)], [("member-owner", 10), (Kept, 20)]),
            // Maria paid 8 dollars for the owner.
            Expense(Kept, [(Kept, 8)], [("member-owner", 8)], currency: "USD"),
            // Square in pounds.
            Expense(Kept, [(Kept, 6)], [(Kept, 6)], currency: "GBP"),
        };

        var transfers = new List<GroupTransfer> { Transfer("member-owner", senderId: Kept, receiverId: "member-owner", amount: 5) };

        var balance = GroupService.GetBalance(Kept, expenses, transfers);

        Assert.Equal(new Dictionary<string, decimal> { ["EUR"] = 15, ["USD"] = -8 }, balance);
    }
}
