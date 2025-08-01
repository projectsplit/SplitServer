﻿using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ITransfersRepository : IRepositoryBase<Transfer>
{
    Task<List<Transfer>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct);

    Task<List<Transfer>> GetAllByGroupId(string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    Task<List<Transfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct);

    Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct);

    Task<List<Transfer>> Search(
        string groupId,
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? receiverIds,
        string[]? senderIds,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct);
}