namespace SplitServer.Models;

public record Invitation : EntityBase
{
    public required string SenderId { get; init; }
    public required string ReceiverId { get; init; }
    public required string GroupId { get; init; }
    public required string? GuestId { get; init; }

    /// <summary>
    /// The member id the receiver gave up by accepting an invitation onto a guest's slot while
    /// already in the group (see <see cref="Services.GuestMerge"/>). Written just before the group
    /// is rewritten, and null for every other invitation. Once the group write has landed the
    /// retired id is nowhere else, so this is what lets a second accept find the records still
    /// filed under it when the first one stopped half-way.
    /// </summary>
    public string? ReplacedMemberId { get; init; }
}