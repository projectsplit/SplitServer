using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IDonationSubscriptionsRepository : IRepositoryBase<DonationSubscription>
{
    Task<bool> HasActiveByUserId(string userId, CancellationToken ct);

    Task<List<DonationSubscription>> GetByUserId(string userId, CancellationToken ct);
}
