using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// Finds the non-group records a deleted account leaves with nobody able to see them.
///
/// An expense or transfer made directly between people has no group to belong to. It exists for as
/// long as someone who took part can still open it, and is kept through one person's deletion
/// because it is the other person's record too. Once the account being deleted is the last one in
/// it — it was only ever theirs, or everyone else's account has already gone — it has no reader
/// left, and goes with the account rather than staying on as a record of two people who both asked
/// to be forgotten.
/// </summary>
public static class OrphanedNonGroupRecords
{
    /// <summary>Everyone else these records name, which is who to look up before deciding.</summary>
    public static List<string> OtherUserIds(
        IEnumerable<NonGroupExpense> expenses,
        IEnumerable<NonGroupTransfer> transfers,
        string userId)
    {
        return expenses.SelectMany(Participants)
            .Concat(transfers.SelectMany(Participants))
            .Where(x => x != userId)
            .Distinct()
            .ToList();
    }

    public static List<string> ExpenseIds(
        IEnumerable<NonGroupExpense> expenses,
        string userId,
        IReadOnlySet<string> existingUserIds)
    {
        return expenses
            .Where(x => !HasAnotherReader(Participants(x), userId, existingUserIds))
            .Select(x => x.Id)
            .ToList();
    }

    public static List<string> TransferIds(
        IEnumerable<NonGroupTransfer> transfers,
        string userId,
        IReadOnlySet<string> existingUserIds)
    {
        return transfers
            .Where(x => !HasAnotherReader(Participants(x), userId, existingUserIds))
            .Select(x => x.Id)
            .ToList();
    }

    private static bool HasAnotherReader(IEnumerable<string> participants, string userId, IReadOnlySet<string> existingUserIds) =>
        participants.Any(x => x != userId && existingUserIds.Contains(x));

    private static IEnumerable<string> Participants(NonGroupExpense expense) =>
        expense.Payments.Select(x => x.UserId).Concat(expense.Shares.Select(x => x.UserId));

    private static IEnumerable<string> Participants(NonGroupTransfer transfer) =>
        [transfer.SenderId, transfer.ReceiverId];
}
