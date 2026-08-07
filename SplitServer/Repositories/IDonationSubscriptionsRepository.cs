using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IDonationSubscriptionsRepository : IRepositoryBase<DonationSubscription>
{
    Task<bool> HasActiveByUserId(string userId, CancellationToken ct);
}
