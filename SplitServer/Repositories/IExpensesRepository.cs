using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Queries.Models;

namespace SplitServer.Repositories;

public interface IExpensesRepository : IRepositoryBase<Expense>
{
    Task<List<GroupExpense>> GetByGroupId(
        string groupId,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<GroupExpense>> GetGroupExpensesByGroupId(string groupId, CancellationToken ct);

    Task<List<NonGroupExpense>> GetNonGroupExpensesByUserId(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    Task<List<Expense>> GetPersonalExpensesByUserId(
        string userId,
        List<string> memberIds,
        CancellationToken ct,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    /// <summary>
    /// Unmarks every expense a deleted series produced. They stay as ordinary expenses — real
    /// spending that happened — but stop claiming to belong to a schedule that no longer exists.
    /// </summary>
    Task<Result> ClearRecurringExpenseId(string recurringExpenseId, CancellationToken ct);

    Task<List<GroupExpense>> GetGroupExpensesByMemberIds(
        List<string> memberIds,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct);

    Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct);

    Task<bool> UserLabelInUse(string labelText, CancellationToken ct);

    Task<List<GroupExpense>> Search(
        string groupId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? participantIds,
        string[]? payerIds,
        string[]? labelIds,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<NonGroupExpense>> GetNonGroupByUserId(
        string userId,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<Expense>> GetPersonalByUserId(
        string userId,
        List<string> memberIds,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<Expense>> SearchPersonalByUserId(
        string userId,
        List<string> memberIds,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? labels,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<NonGroupExpense>> SearchNonGroup(
        string userId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? participantIds,
        string[]? payerIds,
        string[]? labelIds,
        int pageSize,
        DateTime? occurred,
        DateTime? created,
        PaginationDirection direction,
        bool inclusive,
        CancellationToken ct);

    Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// How many expenses this person has entered, of any kind, giving up once <paramref name="limit"/>
    /// have been found. Callers only ever want to know whether a threshold was crossed, and stopping
    /// there keeps the count off the profile of someone with years of history.
    /// </summary>
    Task<long> CountByCreatorId(string creatorId, int limit, CancellationToken ct);
}