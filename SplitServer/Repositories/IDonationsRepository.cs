using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IDonationsRepository : IRepositoryBase<Donation>
{
    Task<List<Donation>> GetByUserId(string userId, CancellationToken ct);

    /// <summary>
    /// Finds a gift by the Play purchase token that produced it. Needed because a refund
    /// notification names only the token, and the product id — which the Play API insists on before
    /// it will say anything — was written down here at purchase time.
    /// </summary>
    Task<Maybe<Donation>> GetByPurchaseToken(string purchaseToken, CancellationToken ct);
}
