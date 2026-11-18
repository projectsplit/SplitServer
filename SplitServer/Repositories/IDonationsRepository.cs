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

    /// <summary>
    /// Writes what was actually paid onto one gift and touches nothing else on the row. A
    /// field-level write, because it lands after the gift has been recorded and must not put back a
    /// status that a refund arriving in the meantime has just changed.
    /// </summary>
    Task<Result> SetCharged(string id, decimal amount, string currency, CancellationToken ct);

    /// <summary>
    /// The id of everyone with at least one gift that arrived. One distinct read rather than the
    /// ledger itself, since all the caller wants is whose summaries to bring up to date.
    /// </summary>
    Task<List<string>> GetDonorUserIds(CancellationToken ct);
}
