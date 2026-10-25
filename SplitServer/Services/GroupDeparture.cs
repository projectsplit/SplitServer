using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// What a group becomes when the account behind one of its members is deleted.
///
/// A group's ledger belongs to everyone in it, so one person leaving for good must not rewrite what
/// the others are owed. The member's id is what every expense and transfer points at, and it stays:
/// it is handed to a guest slot, exactly as when someone leaves a group by choice. Only someone who
/// never appeared in the ledger is removed outright.
/// </summary>
public static class GroupDeparture
{
    /// <summary>
    /// The group expenses that concern nobody but the departing member: they alone paid, and they
    /// alone carry the cost. Filed in a group, but a personal expense in everything else — nobody
    /// else's balance moves when one goes — so it is deleted with the account rather than kept on
    /// under a guest's name, where it would be a record of one person's spending left on display
    /// for people it never involved.
    /// </summary>
    public static List<string> ExpenseIdsConcerningOnly(IEnumerable<GroupExpense> expenses, string memberId)
    {
        return expenses
            // "All" is true of an empty list, so an expense with no payments and no shares would
            // pass both tests below without the member appearing in it at all.
            .Where(x => x.Payments.Count + x.Shares.Count > 0)
            .Where(x => x.Payments.All(p => p.MemberId == memberId))
            .Where(x => x.Shares.All(s => s.MemberId == memberId))
            .Select(x => x.Id)
            .ToList();
    }

    /// <returns>
    /// The group as it should now be stored, or null when nobody with an account is left in it. A
    /// group like that was only ever this person's own bookkeeping — guests have no account to see
    /// it from — so the caller deletes it rather than keeping it for no one.
    /// </returns>
    public static Group? WithoutMember(Group group, Member member, string username, bool memberHasActivity, DateTime now)
    {
        var remainingMembers = group.Members.Where(x => x.Id != member.Id).ToList();

        if (remainingMembers.Count == 0)
        {
            return null;
        }

        var guests = memberHasActivity
            ? group.Guests.Concat([new Guest { Id = member.Id, Name = $"{username}-guest", Joined = member.Joined }]).ToList()
            : group.Guests;

        return group with
        {
            // Only the owner can delete a group, so it cannot be left owned by an account that no
            // longer exists. Whoever has been in it longest is the least surprising heir.
            OwnerId = group.OwnerId == member.UserId
                ? remainingMembers.MinBy(x => x.Joined)!.UserId
                : group.OwnerId,
            Members = remainingMembers,
            Guests = guests,
            Updated = now,
        };
    }
}
