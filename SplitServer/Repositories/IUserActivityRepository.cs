using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUserActivityRepository : IRepositoryBase<UserActivity>
{
    Task<Result> ClearRecentGroupForUser(string userId, string groupId, DateTime updated, CancellationToken ct);

    Task<Result> ClearRecentGroupForAllUsers(string groupId, DateTime updated, CancellationToken ct);
}