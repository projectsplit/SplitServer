using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Repositories;

namespace SplitServer.Tests;

/// <summary>
/// The user picker lists the people you are connected to before everyone else, and the two halves
/// are paged separately. What these cover is that scrolling through that seam still shows every
/// user exactly once: a page that spans both halves is where an offset is easiest to lose.
/// <para>
/// The signed-in user is one of the results, as they have always been — the picker shows them as
/// "You" and both sides of a transfer can be you — so they turn up in the unconnected half.
/// </para>
/// </summary>
public class SearchAllUsersQueryHandlerTests
{
    private const string CurrentUserId = "me";

    [Fact]
    public async Task Connections_come_before_everyone_else()
    {
        var handler = BuildHandler(userCount: 10, connectedUserIds: ["user-3", "user-7"]);

        var page = await Page(handler, pageSize: 5);

        Assert.Equal(["user-3", "user-7", CurrentUserId, "user-1", "user-2"], page.Users.Select(x => x.UserId));
    }

    [Fact]
    public async Task Paging_walks_every_user_once_across_the_seam()
    {
        var handler = BuildHandler(userCount: 10, connectedUserIds: ["user-3", "user-7"]);

        var seen = await AllPages(handler, pageSize: 4);

        Assert.Equal(11, seen.Count);
        Assert.Equal(11, seen.Distinct().Count());
        Assert.Equal(["user-3", "user-7"], seen.Take(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Paging_walks_every_user_once_at_any_page_size(int pageSize)
    {
        var handler = BuildHandler(userCount: 10, connectedUserIds: ["user-2", "user-5", "user-9"]);

        var seen = await AllPages(handler, pageSize);

        Assert.Equal(11, seen.Count);
        Assert.Equal(11, seen.Distinct().Count());
        Assert.Equal(["user-2", "user-5", "user-9"], seen.Take(3));
    }

    /// <summary>
    /// A page that ends exactly on the last connection is the case that used to end the list early:
    /// the connections half returns a full page, so nothing signals that another half is waiting.
    /// </summary>
    [Fact]
    public async Task Connections_that_exactly_fill_a_page_do_not_end_the_results()
    {
        var handler = BuildHandler(userCount: 10, connectedUserIds: ["user-1", "user-2", "user-3", "user-4"]);

        var seen = await AllPages(handler, pageSize: 4);

        Assert.Equal(11, seen.Count);
        Assert.Equal(11, seen.Distinct().Count());
    }

    [Fact]
    public async Task No_connections_leaves_the_plain_list_untouched()
    {
        var handler = BuildHandler(userCount: 6, connectedUserIds: []);

        var seen = await AllPages(handler, pageSize: 4);

        Assert.Equal([CurrentUserId, "user-1", "user-2", "user-3", "user-4", "user-5", "user-6"], seen);
    }

    [Fact]
    public async Task Everyone_connected_still_terminates()
    {
        var handler = BuildHandler(userCount: 4, connectedUserIds: ["user-1", "user-2", "user-3", "user-4"]);

        var seen = await AllPages(handler, pageSize: 3);

        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task Keyword_search_orders_connections_first_too()
    {
        var handler = BuildHandler(userCount: 10, connectedUserIds: ["user-7"]);

        var page = await Page(handler, pageSize: 5, keyword: "user-1");

        // Only user-1 and user-10 match, and neither is connected, so the connected half is empty.
        Assert.Equal(["user-1", "user-10"], page.Users.Select(x => x.UserId));
    }

    private static SearchAllUsersQueryHandler BuildHandler(int userCount, string[] connectedUserIds)
    {
        var users = Enumerable
            .Range(1, userCount)
            .Select(
                i => new User
                {
                    Id = $"user-{i}",
                    Username = $"user-{i}",
                    Email = null,
                    EmailVerified = false,
                    HashedPassword = null,
                    GoogleId = null,
                    Created = DateTime.UnixEpoch,
                    Updated = DateTime.UnixEpoch
                })
            .ToList();

        var me = new User
        {
            Id = CurrentUserId,
            Username = CurrentUserId,
            Email = null,
            EmailVerified = false,
            HashedPassword = null,
            GoogleId = null,
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };

        return new SearchAllUsersQueryHandler(
            new FakeUsersRepository([me, .. users]),
            new FakeUserConnectionsRepository(connectedUserIds));
    }

    private static async Task<SplitServer.Responses.SearchAllUsersResponse> Page(
        SearchAllUsersQueryHandler handler,
        int pageSize,
        string? keyword = null,
        string? next = null)
    {
        var result = await handler.Handle(
            new SearchAllUsersQuery
            {
                UserId = CurrentUserId,
                PageSize = pageSize,
                Keyword = keyword,
                Next = next
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);

        return result.Value;
    }

    private static async Task<List<string>> AllPages(
        SearchAllUsersQueryHandler handler,
        int pageSize,
        string? keyword = null)
    {
        var seen = new List<string>();
        string? next = null;

        // Bounded so a cursor that never advances fails as a test rather than hanging the suite.
        for (var request = 0; request < 100; request++)
        {
            var page = await Page(handler, pageSize, keyword, next);

            seen.AddRange(page.Users.Select(x => x.UserId));

            if (page.Next is null)
            {
                return seen;
            }

            next = page.Next;
        }

        Assert.Fail("Paging did not terminate");

        return seen;
    }
}

/// <summary>
/// Serves the pages the way Mongo does — filter, then skip, then limit — so the handler's
/// bookkeeping between the two halves is what is under test.
/// </summary>
file class FakeUsersRepository : IUsersRepository
{
    private readonly List<User> _users;

    public FakeUsersRepository(List<User> users) => _users = users;

    public Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct)
    {
        var matches = InScope(scope).Where(x => x.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(matches.Skip(skip).Take(pageSize).ToList());
    }

    public Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct)
    {
        return Task.FromResult(InScope(scope).Skip(skip).Take(pageSize).ToList());
    }

    public Task<Maybe<User>> GetById(string id, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(user is not null ? user : Maybe<User>.None);
    }

    private IEnumerable<User> InScope(UserIdScope scope) => scope.Kind switch
    {
        UserIdScopeKind.Only => _users.Where(x => scope.Ids.Contains(x.Id)),
        UserIdScopeKind.Except => _users.Where(x => !scope.Ids.Contains(x.Id)),
        _ => _users
    };

    public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> AnyWithUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(User updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeUserConnectionsRepository : IUserConnectionsRepository
{
    private readonly List<string> _acceptedUserIds;

    public FakeUserConnectionsRepository(string[] acceptedUserIds) => _acceptedUserIds = [.. acceptedUserIds];

    public Task<List<string>> GetAcceptedUserIds(string userId, CancellationToken ct) => Task.FromResult(_acceptedUserIds);

    public Task<Maybe<UserConnection>> GetBetweenUsers(string userIdA, string userIdB, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<UserConnection>> GetAllBetweenUsers(string userId, IList<string> otherUserIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<UserConnection>> GetPendingByReceiverId(string receiverId, int pageSize, DateTime maxCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<long> CountPendingByReceiverIdAndMinCreated(string receiverId, DateTime minCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<UserConnection>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<UserConnection>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserConnection entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserConnection> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserConnection entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserConnection updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}
