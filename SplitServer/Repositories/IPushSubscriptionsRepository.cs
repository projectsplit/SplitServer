using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IPushSubscriptionsRepository : IRepositoryBase<PushSubscription>
{
    Task<List<PushSubscription>> GetAllByUserId(string userId, CancellationToken ct);

    Task<List<PushSubscription>> GetAllByUserIds(IList<string> userIds, CancellationToken ct);

    Task<Maybe<PushSubscription>> GetByEndpoint(string endpoint, CancellationToken ct);

    Task<Result> DeleteByEndpoint(string endpoint, CancellationToken ct);
}
