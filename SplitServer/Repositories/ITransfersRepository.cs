using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface ITransfersRepository : IRepositoryBase<Transfer>
{
    Task<List<Transfer>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccured, DateTime? maxCreated, CancellationToken ct);
    
    Task<List<Transfer>> GetAllByGroupId(string groupId, CancellationToken ct);
    
    Task<Result> SoftDeleteByGroupId(string groupId, CancellationToken ct);
    
    Task<List<Transfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct);
}