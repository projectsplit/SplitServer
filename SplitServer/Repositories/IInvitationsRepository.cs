using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IInvitationsRepository : IRepositoryBase<Invitation>
{
    Task<Maybe<Invitation>> Get(string fromId, string toId, string groupId, CancellationToken ct);
    
    Task<Maybe<Invitation>> GetByToId(string toId, string groupId, CancellationToken ct);
    
    Task<Maybe<Invitation>> GetByGuestId(string guestId, string groupId, CancellationToken ct);
    
    Task<Result> DeleteByGroupId(string groupId, CancellationToken ct);
}