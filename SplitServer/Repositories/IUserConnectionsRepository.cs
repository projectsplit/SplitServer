using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUserConnectionsRepository : IRepositoryBase<UserConnection>
{
    Task<Maybe<UserConnection>> GetBetweenUsers(string userIdA, string userIdB, CancellationToken ct);

    Task<List<UserConnection>> GetAllBetweenUsers(string userId, IList<string> otherUserIds, CancellationToken ct);

    Task<List<string>> GetAcceptedUserIds(string userId, CancellationToken ct);

    Task<List<UserConnection>> GetPendingByReceiverId(string receiverId, int pageSize, DateTime maxCreatedDate, CancellationToken ct);

    Task<long> CountPendingByReceiverIdAndMinCreated(string receiverId, DateTime minCreatedDate, CancellationToken ct);
}
