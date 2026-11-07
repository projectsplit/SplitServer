using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// How a guest and the account that turns out to be them become one member.
/// <para>
/// A guest is a placeholder the group tracks until the real person shows up. When they arrive on an
/// invitation onto that guest's slot, they inherit its id and every record behind it (see
/// <see cref="Guest"/>). Someone who came in through the group's join link already has a member id
/// of their own, with records of its own, so there is no empty slot to step into: the two ids have
/// to be folded into one. The guest's id is the one kept — it is the one the invitation names and
/// the one the older history points at — and the person's own id is retired, every record carrying
/// it rewritten to the guest's.
/// </para>
/// <para>
/// The group write is the commit point. It drops the guest and the person's old member in one
/// conditional replace, so two acceptances for the same guest cannot both win, and once it has
/// landed the retired id is nowhere in the group, so nothing new can be filed under it. The records
/// are rewritten after that. Each says which ids it carries, so the rewrite can be repeated until it
/// is complete, and the invitation stays until then, holding the retired id
/// (<see cref="Invitation.ReplacedMemberId"/>) so a second accept knows what to look for.
/// </para>
/// </summary>
public static class GuestMerge
{
    public const string UnfinishedError = "This invitation is half-way through replacing a guest. Accept it to finish";

    /// <summary>
    /// The group once a member has taken a guest's slot: the guest is gone, the member's old entry
    /// is gone, and the member stands under the guest's id, keeping their own join date.
    /// </summary>
    public static Group Claim(Group group, Member member, Guest guest, DateTime now)
    {
        return group with
        {
            Members = group.Members.Where(x => x.Id != member.Id).Append(member with { Id = guest.Id }).ToList(),
            Guests = group.Guests.Where(x => x.Id != guest.Id).ToList(),
            Updated = now,
        };
    }

    /// <summary>The group once someone not yet in it has taken a guest's slot: the guest becomes a member under the same id.</summary>
    public static Group TakeOver(Group group, Guest guest, string userId, DateTime now)
    {
        return group with
        {
            Members = group.Members.Append(new Member { Id = guest.Id, UserId = userId, Joined = now }).ToList(),
            Guests = group.Guests.Where(x => x.Id != guest.Id).ToList(),
            Updated = now,
        };
    }

    /// <summary>
    /// Whether the group write of a merge has landed while the invitation is still there, which
    /// means the records may not all be rewritten yet. Only accepting again finishes it; anything
    /// else that removed the invitation would take the retired id with it, and the records still
    /// filed under that id would be nobody's.
    /// </summary>
    public static bool IsUnfinished(Invitation invitation, Group group)
    {
        return invitation.ReplacedMemberId is not null
               && group.Members.Any(x => x.UserId == invitation.ReceiverId && x.Id == invitation.GuestId);
    }

    /// <summary>
    /// The expense with every mention of the retired id rewritten to the kept one. Where both ids
    /// appeared — the person paid and their guest had a share, say — the two lines become one
    /// carrying the sum, because an expense may list a member only once.
    /// </summary>
    public static GroupExpense Rewrite(GroupExpense expense, string retiredId, string keptId, DateTime now)
    {
        return expense with
        {
            CreatorId = Rewrite(expense.CreatorId, retiredId, keptId),
            Payments = Rewrite(expense.Payments, retiredId, keptId),
            Shares = Rewrite(expense.Shares, retiredId, keptId),
            Updated = now,
        };
    }

    public static GroupRecurringExpense Rewrite(GroupRecurringExpense template, string retiredId, string keptId, DateTime now)
    {
        return template with
        {
            Payments = Rewrite(template.Payments, retiredId, keptId),
            Shares = Rewrite(template.Shares, retiredId, keptId),
            Updated = now,
        };
    }

    /// <summary>
    /// The transfer with the retired id rewritten, or null when that would make the sender and the
    /// receiver the same person. Such a transfer only ever moved money between the two ids being
    /// merged; it changes nobody's balance once they are one, so it is deleted rather than kept as
    /// a payment to oneself.
    /// </summary>
    public static GroupTransfer? Rewrite(GroupTransfer transfer, string retiredId, string keptId, DateTime now)
    {
        var senderId = Rewrite(transfer.SenderId, retiredId, keptId);
        var receiverId = Rewrite(transfer.ReceiverId, retiredId, keptId);

        if (senderId == receiverId)
        {
            return null;
        }

        return transfer with
        {
            CreatorId = Rewrite(transfer.CreatorId, retiredId, keptId),
            SenderId = senderId,
            ReceiverId = receiverId,
            Updated = now,
        };
    }

    private static List<GroupPayment> Rewrite(List<GroupPayment> payments, string retiredId, string keptId)
    {
        return payments
            .Select(x => x with { MemberId = Rewrite(x.MemberId, retiredId, keptId) })
            .GroupBy(x => x.MemberId)
            .Select(g => g.First() with { Amount = g.Sum(x => x.Amount) })
            .ToList();
    }

    private static List<GroupShare> Rewrite(List<GroupShare> shares, string retiredId, string keptId)
    {
        return shares
            .Select(x => x with { MemberId = Rewrite(x.MemberId, retiredId, keptId) })
            .GroupBy(x => x.MemberId)
            .Select(g => g.First() with { Amount = g.Sum(x => x.Amount) })
            .ToList();
    }

    private static string Rewrite(string id, string retiredId, string keptId)
    {
        return id == retiredId ? keptId : id;
    }
}
