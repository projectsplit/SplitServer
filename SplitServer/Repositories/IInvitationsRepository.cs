using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IInvitationsRepository : IRepositoryBase<Invitation>
{
    Task<Maybe<Invitation>> Get(string senderId, string receiverId, string groupId, CancellationToken ct);

    Task<List<Invitation>> GetByReceiverId(string receiverId, int pageSize, DateTime maxCreatedDate, CancellationToken ct);

    Task<List<Invitation>> GetByReceiverIds(List<string> receiverIds, string groupId, CancellationToken ct);

    Task<Maybe<Invitation>> GetByGroupIdAndReceiverId(string receiverId, string groupId, CancellationToken ct);

    Task<Maybe<Invitation>> GetByGuestId(string guestId, string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupIdAndReceiverId(string receiverId, string groupId, CancellationToken ct);

    Task<Result> DeleteByGroupIdAndSenderId(string senderId, string groupId, CancellationToken ct);

    Task<Result> DeleteByGuestId(string guestId, string groupId, CancellationToken ct);

    Task<long> CountByReceiverIdAndMinCreated(string receiverId, DateTime minCreatedDate, CancellationToken ct);
}