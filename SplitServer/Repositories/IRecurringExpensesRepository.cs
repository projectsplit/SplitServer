using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IRecurringExpensesRepository : IRepositoryBase<RecurringExpense>
{
    Task<List<RecurringExpense>> GetAllByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// Templates whose next occurrence has come due. Paused ones are excluded here rather than by
    /// the caller so a paused template costs nothing on every tick.
    /// </summary>
    Task<List<RecurringExpense>> GetDue(DateTime nowUtc, int limit, CancellationToken ct);

    /// <summary>
    /// Replaces the template only if nobody has written to it since it was read, identified by the
    /// Updated stamp on the copy that was read. False means a concurrent write got there first and
    /// the caller must re-read and reconcile rather than overwrite it — the worker holds a template
    /// for as long as an occurrence takes to create, and a pause or edit landing in that window
    /// must not be silently undone.
    /// </summary>
    Task<Result<bool>> UpdateIfUnchanged(RecurringExpense entity, DateTime expectedUpdated, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    Task EnsureIndexes(CancellationToken ct);
}
