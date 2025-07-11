﻿using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IExpensesRepository : IRepositoryBase<Expense>
{
    Task<List<Expense>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct);

    Task<List<Expense>> GetAllByGroupId(string groupId, CancellationToken ct);

    Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct);

    Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, DateTime startDate, DateTime endDate, CancellationToken ct);

    Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct);

    Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct);

    Task<List<Expense>> Search(
        string groupId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? participantIds,
        string[]? payerIds,
        string[]? labelIds,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct);
}