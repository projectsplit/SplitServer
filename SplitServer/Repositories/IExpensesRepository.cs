using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories;

public interface IExpensesRepository : IRepositoryBase<Expense>
{
    Task<List<Expense>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccured, DateTime? maxCreated, CancellationToken ct);
    
    Task<List<Expense>> GetAllByGroupId(string groupId, CancellationToken ct);

    Task<List<LabelCount>> GetAllLabels(string groupId, CancellationToken ct);
    
    Task<Result> SoftDeleteByGroupId(string groupId, CancellationToken ct);
    
    Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct);
    
    Task<List<Expense>> GetAllByMemberIds(List<string> memberIds, DateTime startDate, DateTime endDate, CancellationToken ct);
}