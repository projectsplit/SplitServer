namespace SplitServer.Responses;

public class InvitationResponseItem
{
    public required string Id { get; init; }
    public required DateTime Created { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string GroupId { get; init; }
    public required string GroupName { get; init; }
    public required string? GuestId { get; init; }
    public required string? GuestName { get; init; }

    /// <summary>
    /// What the guest owes (positive) or is owed (negative) in each currency — what accepting takes
    /// on. Null when the invitation names no guest, or a guest who is no longer there.
    /// </summary>
    public required Dictionary<string, decimal>? GuestBalance { get; init; }

    /// <summary>
    /// Whether the receiver is already in the group, so that accepting merges the guest into them
    /// instead of adding them. Nothing about a merge can be undone, and the receiver should know
    /// that before they tap.
    /// </summary>
    public required bool IsMerge { get; init; }
}