namespace SplitServer.Models;

/// <summary>
/// A participant the group tracks who has no account of their own — either someone who never had
/// one, or a member who left and whose id was handed over so the group's ledger keeps working.
/// <para>
/// A guest belongs to the group, not to a person. Nothing here links back to a user account, and
/// that is what stops a departed member's spending counting towards them while they are gone. The
/// link is restored by inviting them onto this guest slot, which returns the id to their
/// membership and brings every expense and transfer behind it back with it.
/// </para>
/// </summary>
public class Guest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DateTime Joined { get; init; }
}
