using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IPlusSubscriptionsRepository : IRepositoryBase<PlusSubscription>
{
    /// <summary>
    /// Every subscription this person has ever had, live or not. Whether they have Plus is decided
    /// across all of them, because resubscribing after a lapse is a new row rather than a change to
    /// the old one.
    /// </summary>
    Task<List<PlusSubscription>> GetByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// Every subscription Play might still have something new to say about: all of them except the
    /// ones in <paramref name="endedStates"/>, which never change again. This is what the worker
    /// walks, so a finished subscription costs nothing on any pass.
    /// </summary>
    Task<List<PlusSubscription>> GetNotInStates(IReadOnlyCollection<string> endedStates, CancellationToken ct);

    /// <summary>
    /// Marks the renewal reminder for the period ending at <paramref name="expiresAt"/> as sent, and
    /// says whether this call was the one that did it. True means the caller owns the reminder and
    /// should send it; false means it has gone out already, or the row is gone.
    /// </summary>
    /// <remarks>
    /// One conditional write rather than a read followed by a write, so two passes crossing — a
    /// restart mid-pass, a second instance — cannot both decide the reminder is theirs. Claimed
    /// before sending rather than after: a crash in between loses one reminder, where the other
    /// order would send the same one twice.
    /// </remarks>
    Task<bool> TryClaimRenewalReminder(string id, DateTime expiresAt, CancellationToken ct);

    Task EnsureIndexes(CancellationToken ct);
}
