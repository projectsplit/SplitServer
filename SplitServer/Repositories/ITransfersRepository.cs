using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ITransfersRepository : IRepositoryBase<Transfer>
{
    Task<List<GroupTransfer>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct);
    
    Task<List<NonGroupTransfer>> GetByUserId(string userId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct);
    
    Task<List<GroupTransfer>> GetAllByGroupId(string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    Task<List<GroupTransfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct);

    Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct);

    Task<List<GroupTransfer>> Search(
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
    
    Task<List<NonGroupTransfer>> SearchNonGroup(
        string? searchTerm,
        DateTime? minTime,
        DateTime? maxTime,
        string[]? receiverIds,
        string[]? senderIds,
        int pageSize,
        DateTime? maxOccurred,
        DateTime? maxCreated,
        CancellationToken ct);

    Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct);
}