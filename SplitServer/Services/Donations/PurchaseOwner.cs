using CSharpFunctionalExtensions;
using Serilog;

namespace SplitServer.Services.Donations;

/// <summary>
/// Decides who a Google Play purchase belongs to. One rule for tips and for Buqs Plus, kept in one
/// place because it is the check that stops one account claiming what another paid for, and two
/// copies of a rule like that eventually disagree.
/// </summary>
public static class PurchaseOwner
{
    /// <summary>
    /// The account a purchase is credited to, or a failure when that cannot be settled.
    /// </summary>
    /// <param name="playUserId">
    /// The account id that came back from Google with the purchase, or failing that the one written
    /// down when the purchase was first recorded. Either way it did not come from the caller.
    /// </param>
    /// <param name="expectedUserId">The signed-in caller, when there is one. A notification has none.</param>
    /// <remarks>
    /// Play's own account id wins, because it is what the app set when it launched the purchase and
    /// came back through Google rather than through the caller. A caller claiming a purchase Play
    /// attributes to someone else is refused outright: that is exactly the shape of stealing
    /// somebody's purchase by replaying their token.
    /// </remarks>
    public static Result<string> Resolve(string? playUserId, string? expectedUserId)
    {
        if (playUserId is null)
        {
            return expectedUserId is not null
                ? expectedUserId
                : Result.Failure<string>("Purchase has no account attached");
        }

        if (expectedUserId is not null && playUserId != expectedUserId)
        {
            Log.Warning(
                "User {CallerId} tried to register a purchase Google Play attributes to {OwnerId}",
                expectedUserId,
                playUserId);

            return Result.Failure<string>("Purchase belongs to another account");
        }

        return playUserId;
    }
}
