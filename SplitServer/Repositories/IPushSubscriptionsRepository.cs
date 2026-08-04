using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IPushSubscriptionsRepository : IRepositoryBase<PushSubscription>
{
    Task<List<PushSubscription>> GetAllByUserIds(IList<string> userIds, CancellationToken ct);

    Task<Result> DeleteByEndpoint(string endpoint, CancellationToken ct);
}
