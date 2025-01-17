using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IInvitationsRepository : IRepositoryBase<Invitation>
{
    Task<Maybe<Invitation>> Get(string fromId, string toId, string groupId, CancellationToken ct);
}