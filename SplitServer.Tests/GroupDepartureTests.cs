using SplitServer.Models;
using SplitServer.Services;

namespace SplitServer.Tests;

public class GroupDepartureTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Member Member(string userId, int joinedDaysAgo) =>
        new() { Id = $"member-{userId}", UserId = userId, Joined = Now.AddDays(-joinedDaysAgo) };

    private static Group Group(string ownerId, Member[] members, Guest[]? guests = null) =>
        new()
        {
            Id = "group",
            Created = Now.AddDays(-100),
            Updated = Now.AddDays(-1),
            OwnerId = ownerId,
            Name = "Flat",
            Currency = "EUR",
            IsArchived = false,
            Members = [.. members],
            Guests = [.. guests ?? []],
            Labels = [],
        };

    [Fact]
    public void A_member_the_ledger_mentions_becomes_a_guest_with_the_same_id()
    {
        var leaving = Member("leaving", joinedDaysAgo: 30);
        var group = Group("owner", [Member("owner", 90), leaving]);

        var result = GroupDeparture.WithoutMember(group, leaving, "kristi", memberHasActivity: true, Now);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Members, x => x.UserId == "leaving");

        var guest = Assert.Single(result.Guests);

        // The id is what every expense and transfer points at; keeping it is the whole point.
        Assert.Equal(leaving.Id, guest.Id);
        Assert.Equal("kristi-guest", guest.Name);
        Assert.Equal(leaving.Joined, guest.Joined);
        Assert.Equal(Now, result.Updated);
    }

    [Fact]
    public void A_member_the_ledger_never_mentions_is_removed_without_a_trace()
    {
        var leaving = Member("leaving", joinedDaysAgo: 30);
        var existingGuest = new Guest { Id = "guest", Name = "Sam", Joined = Now.AddDays(-50) };
        var group = Group("owner", [Member("owner", 90), leaving], [existingGuest]);

        var result = GroupDeparture.WithoutMember(group, leaving, "kristi", memberHasActivity: false, Now);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Members, x => x.UserId == "leaving");
        Assert.Equal(existingGuest, Assert.Single(result.Guests));
    }

    [Fact]
    public void An_owner_who_leaves_hands_the_group_to_whoever_has_been_in_it_longest()
    {
        var owner = Member("owner", joinedDaysAgo: 90);
        var group = Group("owner", [owner, Member("newer", 10), Member("older", 60)]);

        var result = GroupDeparture.WithoutMember(group, owner, "kristi", memberHasActivity: true, Now);

        Assert.NotNull(result);
        Assert.Equal("older", result.OwnerId);
    }

    [Fact]
    public void Someone_else_leaving_does_not_change_who_owns_the_group()
    {
        var leaving = Member("leaving", joinedDaysAgo: 80);
        var group = Group("owner", [Member("owner", 90), leaving]);

        var result = GroupDeparture.WithoutMember(group, leaving, "kristi", memberHasActivity: true, Now);

        Assert.NotNull(result);
        Assert.Equal("owner", result.OwnerId);
    }

    [Fact]
    public void A_group_with_nobody_else_who_has_an_account_is_not_kept()
    {
        var owner = Member("owner", joinedDaysAgo: 90);
        var guest = new Guest { Id = "guest", Name = "Sam", Joined = Now.AddDays(-50) };
        var group = Group("owner", [owner], [guest]);

        // Guests cannot sign in, so a group left with only guests would be kept for nobody.
        Assert.Null(GroupDeparture.WithoutMember(group, owner, "kristi", memberHasActivity: true, Now));
    }
}
