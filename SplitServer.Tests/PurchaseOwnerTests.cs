using SplitServer.Services.Donations;

namespace SplitServer.Tests;

/// <summary>
/// Who a Google Play purchase is credited to. This is the check between a purchase token and an
/// account, shared by tips and Buqs Plus, and the case that matters is the one where it must say
/// no: a signed-in caller presenting a token Google attributes to somebody else.
/// </summary>
public class PurchaseOwnerTests
{
    [Fact]
    public void Credits_the_account_Play_says_bought_it()
    {
        var owner = PurchaseOwner.Resolve(playUserId: "user-1", expectedUserId: "user-1");

        Assert.True(owner.IsSuccess);
        Assert.Equal("user-1", owner.Value);
    }

    /// <summary>A notification from Play carries no caller, and Play's word is enough on its own.</summary>
    [Fact]
    public void Needs_no_caller_when_Play_names_the_account()
    {
        var owner = PurchaseOwner.Resolve(playUserId: "user-1", expectedUserId: null);

        Assert.True(owner.IsSuccess);
        Assert.Equal("user-1", owner.Value);
    }

    /// <summary>
    /// The shape of stealing somebody's purchase by replaying their token. Without this, anyone who
    /// got hold of a token could have the Plus it paid for land on their own account.
    /// </summary>
    [Fact]
    public void Refuses_a_caller_claiming_a_purchase_Play_attributes_to_someone_else()
    {
        var owner = PurchaseOwner.Resolve(playUserId: "user-1", expectedUserId: "user-2");

        Assert.True(owner.IsFailure);
        Assert.Equal("Purchase belongs to another account", owner.Error);
    }

    [Fact]
    public void Falls_back_to_the_caller_when_Play_names_nobody()
    {
        var owner = PurchaseOwner.Resolve(playUserId: null, expectedUserId: "user-2");

        Assert.True(owner.IsSuccess);
        Assert.Equal("user-2", owner.Value);
    }

    [Fact]
    public void Refuses_a_purchase_that_nobody_at_all_can_be_tied_to()
    {
        var owner = PurchaseOwner.Resolve(playUserId: null, expectedUserId: null);

        Assert.True(owner.IsFailure);
        Assert.Equal("Purchase has no account attached", owner.Error);
    }
}
