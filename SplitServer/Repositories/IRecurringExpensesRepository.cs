using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IRecurringExpensesRepository : IRepositoryBase<RecurringExpense>
{
    Task<List<RecurringExpense>> GetAllByUserId(string userId, CancellationToken ct);

    Task<int> CountByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// Templates whose next occurrence has come due. Paused ones are excluded here rather than by
    /// the caller so a paused template costs nothing on every tick.
    /// </summary>
    Task<List<RecurringExpense>> GetDue(DateTime nowUtc, int limit, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);
}
