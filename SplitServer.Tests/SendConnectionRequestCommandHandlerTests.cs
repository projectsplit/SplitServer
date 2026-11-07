using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using SplitServer.Commands;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// Which of the two outcomes leaves a record in the notifications menu. A new request does not need
/// one, because the pending request is shown there itself. A request that answers one already
/// waiting accepts it, and that pending request then disappears from the menu, so the person who
/// asked first needs the same lasting entry as when the request is accepted with the button.
/// </summary>
public class SendConnectionRequestCommandHandlerTests
{
    private const string AliceId = "alice";
    private const string BobId = "bob";

    [Fact]
    public async Task New_request_waits_as_pending_and_writes_no_feed_entry()
    {
        var connections = new FakeUserConnectionsRepository([]);
        var notifications = new FakeNotificationsRepository();
        var handler = BuildHandler(connections, notifications);

        var result = await handler.Handle(Command(from: AliceId, to: BobId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var connection = Assert.Single(connections.Connections);
        Assert.Equal(AliceId, connection.SenderId);
        Assert.Equal(BobId, connection.ReceiverId);
        Assert.Equal(ConnectionStatus.Pending, connection.Status);
        Assert.Empty(notifications.Inserted);
    }

    [Fact]
    public async Task Asking_back_accepts_and_records_it_in_the_first_senders_feed()
    {
        var connections = new FakeUserConnectionsRepository([Pending(from: BobId, to: AliceId)]);
        var notifications = new FakeNotificationsRepository();
        var handler = BuildHandler(connections, notifications);

        var result = await handler.Handle(Command(from: AliceId, to: BobId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectionStatus.Accepted, Assert.Single(connections.Connections).Status);

        var notification = Assert.Single(notifications.Inserted);
        Assert.Equal(BobId, notification.UserId);
        Assert.Equal("Connection accepted", notification.Title);
        Assert.Contains("alice-name", notification.Body);
        Assert.Equal("/shared/nongroup/expenses", notification.Url);
    }

    private static SendConnectionRequestCommandHandler BuildHandler(
        IUserConnectionsRepository connections,
        INotificationsRepository notifications)
    {
        var users = new FakeUsersRepository([User(AliceId), User(BobId)]);

        var connectionService = new ConnectionService(
            connections,
            users,
            new FakeGroupsRepository(),
            new FakeExpensesRepository(),
            new FakeTransfersRepository());

        // No transport is configured, so push is a logged no-op and the feed is all that is written.
        var pushNotificationService = new PushNotificationService(
            Options.Create(new PushNotificationsSettings()),
            new UnusedPushSubscriptionsRepository(),
            new UnusedUserPreferencesRepository());

        return new SendConnectionRequestCommandHandler(
            users,
            connections,
            connectionService,
            new NotificationService(notifications, pushNotificationService),
            pushNotificationService);
    }

    private static SendConnectionRequestCommand Command(string from, string to) => new()
    {
        UserId = from,
        ReceiverId = to,
    };

    private static User User(string id) => new()
    {
        Id = id,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        Email = null,
        EmailVerified = false,
        HashedPassword = null,
        Username = $"{id}-name",
        GoogleId = null,
    };

    private static UserConnection Pending(string from, string to) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Created = DateTime.UtcNow.AddHours(-1),
        Updated = DateTime.UtcNow.AddHours(-1),
        SenderId = from,
        ReceiverId = to,
        Status = ConnectionStatus.Pending,
    };
}

file class FakeUsersRepository : IUsersRepository
{
    private readonly List<User> _users;

    public FakeUsersRepository(List<User> users) => _users = users;

    public Task<Maybe<User>> GetById(string id, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(user is not null ? user : Maybe<User>.None);
    }

    public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> AnyWithUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(User updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// One row per pair whichever way round it was sent, the way the Mongo lookup treats it.
/// </summary>
file class FakeUserConnectionsRepository : IUserConnectionsRepository
{
    public FakeUserConnectionsRepository(List<UserConnection> connections) => Connections = connections;

    public List<UserConnection> Connections { get; }

    public Task<Maybe<UserConnection>> GetBetweenUsers(string userIdA, string userIdB, CancellationToken ct)
    {
        var connection = Connections.FirstOrDefault(x =>
            (x.SenderId == userIdA && x.ReceiverId == userIdB) ||
            (x.SenderId == userIdB && x.ReceiverId == userIdA));

        return Task.FromResult(connection is not null ? connection : Maybe<UserConnection>.None);
    }

    public Task<List<string>> GetAcceptedUserIds(string userId, CancellationToken ct)
    {
        var userIds = Connections
            .Where(x => x.Status == ConnectionStatus.Accepted && (x.SenderId == userId || x.ReceiverId == userId))
            .Select(x => x.SenderId == userId ? x.ReceiverId : x.SenderId)
            .ToList();

        return Task.FromResult(userIds);
    }

    public Task<Result> Insert(UserConnection entity, CancellationToken ct)
    {
        Connections.Add(entity);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> Update(UserConnection updatedEntity, CancellationToken ct)
    {
        var index = Connections.FindIndex(x => x.Id == updatedEntity.Id);
        Connections[index] = updatedEntity;

        return Task.FromResult(Result.Success());
    }

    public Task<List<UserConnection>> GetAllBetweenUsers(string userId, IList<string> otherUserIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<UserConnection>> GetPendingByReceiverId(string receiverId, int pageSize, DateTime maxCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<long> CountPendingByReceiverIdAndMinCreated(string receiverId, DateTime minCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<UserConnection>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<UserConnection>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserConnection> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserConnection entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeNotificationsRepository : INotificationsRepository
{
    public List<Notification> Inserted { get; } = [];

    public Task<Result> InsertMany(IEnumerable<Notification> entities, CancellationToken ct)
    {
        Inserted.AddRange(entities);

        return Task.FromResult(Result.Success());
    }

    public Task<List<Notification>> GetByUserId(string userId, int pageSize, DateTime maxCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<long> CountByUserIdAndMinCreated(string userId, DateTime minCreatedDate, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task EnsureIndexes(CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<Notification>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<Notification>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(Notification entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Notification entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Notification updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// Alice and Bob share no group and no history, so only the connection row decides whether they
/// count as connected.
/// </summary>
file class FakeGroupsRepository : IGroupsRepository
{
    public Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct) => Task.FromResult(new List<Group>());

    public Task<List<Group>> GetByUserId(string userId, bool? isArchived, int pageSize, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Group>> SearchByGroupName(string userId, string keyword, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<Group>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<Group>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(Group entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<Group> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Group entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Group updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeExpensesRepository : IExpensesRepository
{
    public Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct) => Task.FromResult(new List<string>());

    public Task<List<SplitServer.Models.GroupExpense>> GetByGroupId(string groupId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.GroupExpense>> GetGroupExpensesByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.NonGroupExpense>> GetNonGroupExpensesByUserId(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Expense>> GetPersonalExpensesByUserId(string userId, List<string> memberIds, CancellationToken ct, DateTime? startDate = null, DateTime? endDate = null) => throw new NotSupportedException();
    public Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> ClearRecurringExpenseId(string recurringExpenseId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.GroupExpense>> GetGroupExpensesByMemberIds(List<string> memberIds, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> UserLabelInUse(string labelText, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.GroupExpense>> Search(string groupId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.NonGroupExpense>> GetNonGroupByUserId(string userId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> GetPersonalByUserId(string userId, List<string> memberIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> SearchPersonalByUserId(string userId, List<string> memberIds, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? labels, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.NonGroupExpense>> SearchNonGroup(string userId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<long> CountByCreatorId(string creatorId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeletePersonalByCreatorId(string creatorId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<Expense>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<Expense>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(Expense entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<Expense> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Expense entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Expense updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeTransfersRepository : ITransfersRepository
{
    public Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct) => Task.FromResult(new List<string>());

    public Task<List<GroupTransfer>> GetByGroupId(string groupId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> GetByUserId(string userId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> GetAllByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> GetAllByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> Search(string groupId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? receiverIds, string[]? senderIds, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> SearchNonGroup(string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? receiverIds, string[]? senderIds, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<Transfer>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<Transfer>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(Transfer entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<Transfer> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Transfer entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Transfer updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>Never reached: push sending stops before looking anyone up when no transport is set.</summary>
file class UnusedPushSubscriptionsRepository : IPushSubscriptionsRepository
{
    public Task<List<PushSubscription>> GetAllByUserIds(IList<string> userIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByEndpoint(string endpoint, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<PushSubscription>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<PushSubscription>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(PushSubscription entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<PushSubscription> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(PushSubscription entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(PushSubscription updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>Never reached, for the same reason.</summary>
file class UnusedUserPreferencesRepository : IUserPreferencesRepository
{
    public Task<Maybe<UserPreferences>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<UserPreferences>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserPreferences> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserPreferences updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}
