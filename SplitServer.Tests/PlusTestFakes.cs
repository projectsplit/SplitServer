using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Donations;

namespace SplitServer.Tests;

/// <summary>
/// Stands in for Google Play: says what it has been told to say about each token, and nothing about
/// a token it has not been told about — which is what Google being unreachable looks like from here.
/// </summary>
internal class FakePlay : IPlaySubscriptions
{
    private readonly Dictionary<string, PlaySubscriptionPurchase> _purchases = new();

    public bool IsConfigured { get; set; } = true;

    /// <summary>Every token Play was asked about, in order, repeats included.</summary>
    public List<string> Lookups { get; } = [];

    public List<(string ProductId, string PurchaseToken)> Acknowledged { get; } = [];

    public Result AcknowledgeResult { get; set; } = Result.Success();

    /// <summary>Lets a test check that the record of a purchase existed before Play was told to keep the money.</summary>
    public Func<string, bool> RowExists { get; set; } = _ => false;

    public bool RowExistedWhenAcknowledged { get; private set; }

    public void Says(string purchaseToken, PlaySubscriptionPurchase purchase) => _purchases[purchaseToken] = purchase;

    public void SaysNothingAbout(string purchaseToken) => _purchases.Remove(purchaseToken);

    public Task<Result<PlaySubscriptionPurchase>> GetSubscriptionPurchase(string purchaseToken, CancellationToken ct)
    {
        Lookups.Add(purchaseToken);

        return Task.FromResult(
            _purchases.TryGetValue(purchaseToken, out var purchase)
                ? Result.Success(purchase)
                : Result.Failure<PlaySubscriptionPurchase>("Could not verify the purchase. Please try again later."));
    }

    public Task<Result> AcknowledgeSubscription(string productId, string purchaseToken, CancellationToken ct)
    {
        Acknowledged.Add((productId, purchaseToken));
        RowExistedWhenAcknowledged = RowExists(purchaseToken);

        return Task.FromResult(AcknowledgeResult);
    }
}

internal class FakePlusSubscriptionsRepository : IPlusSubscriptionsRepository
{
    public Dictionary<string, PlusSubscription> Rows { get; } = new();

    public Task<Maybe<PlusSubscription>> GetById(string id, CancellationToken ct)
    {
        return Task.FromResult(Rows.TryGetValue(id, out var row) ? row : Maybe<PlusSubscription>.None);
    }

    public Task<Result> Upsert(PlusSubscription entity, CancellationToken ct)
    {
        Rows[entity.Id] = entity;

        return Task.FromResult(Result.Success());
    }

    public Task<List<PlusSubscription>> GetByUserId(string userId, CancellationToken ct)
    {
        return Task.FromResult(Rows.Values.Where(x => x.UserId == userId).ToList());
    }

    public Task<List<PlusSubscription>> GetNotInStates(IReadOnlyCollection<string> endedStates, CancellationToken ct)
    {
        return Task.FromResult(Rows.Values.Where(x => !endedStates.Contains(x.State)).ToList());
    }

    /// <summary>The same compare-and-set the Mongo implementation does in one conditional write.</summary>
    public Task<bool> TryClaimRenewalReminder(string id, DateTime expiresAt, CancellationToken ct)
    {
        if (!Rows.TryGetValue(id, out var row) || row.RenewalReminderSentFor == expiresAt)
        {
            return Task.FromResult(false);
        }

        Rows[id] = row with { RenewalReminderSentFor = expiresAt };

        return Task.FromResult(true);
    }

    public Task EnsureIndexes(CancellationToken ct) => Task.CompletedTask;

    public Task<IList<PlusSubscription>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(PlusSubscription entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<PlusSubscription> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(PlusSubscription updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// The parts of a user that Buqs Plus reads and writes: the subscription mark, the grant of Plus
/// for life, the trial reminder stage and when the account was made. Like the real one, a
/// field-level write to a user who is not there succeeds and creates nobody.
/// </summary>
internal class FakePlusUsersRepository : IUsersRepository
{
    /// <summary>The Plus mark per account. An account is known to this fake if and only if it has an entry here.</summary>
    public Dictionary<string, UserPlus?> Plus { get; } = new();

    public Dictionary<string, DateTime> Created { get; } = new();

    public Dictionary<string, DateTime?> PlusForLife { get; } = new();

    public Dictionary<string, int?> TrialReminderStage { get; } = new();

    /// <summary>How many times a pass went looking for accounts to remind.</summary>
    public int CandidateSearches { get; private set; }

    /// <summary>Accounts made a year ago, which is old enough that their age is never what a test is about.</summary>
    public FakePlusUsersRepository(params string[] userIds)
    {
        foreach (var userId in userIds)
        {
            Add(userId, DateTime.UtcNow.AddYears(-1));
        }
    }

    public void Add(string userId, DateTime created, DateTime? plusForLife = null, int? trialReminderStage = null)
    {
        Plus[userId] = null;
        Created[userId] = created;
        PlusForLife[userId] = plusForLife;
        TrialReminderStage[userId] = trialReminderStage;
    }

    public User Get(string userId) =>
        new()
        {
            Id = userId,
            Created = Created[userId],
            Updated = Created[userId],
            Email = null,
            EmailVerified = false,
            HashedPassword = null,
            Username = $"{userId}-name",
            GoogleId = null,
            Plus = Plus[userId],
            PlusForLife = PlusForLife[userId],
            PlusTrialReminderStage = TrialReminderStage[userId],
        };

    public Task<Maybe<User>> GetById(string id, CancellationToken ct)
    {
        return Task.FromResult(Plus.ContainsKey(id) ? Get(id) : Maybe<User>.None);
    }

    public Task<Result> SetPlus(string userId, UserPlus? plus, CancellationToken ct)
    {
        if (Plus.ContainsKey(userId))
        {
            Plus[userId] = plus;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetPlusForLife(string userId, DateTime? grantedAt, CancellationToken ct)
    {
        if (Plus.ContainsKey(userId))
        {
            PlusForLife[userId] = grantedAt;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<long>> GrantPlusForLifeToAccountsCreatedBefore(DateTime cutOff, DateTime grantedAt, CancellationToken ct)
    {
        var owed = Plus.Keys.Where(x => Created[x] < cutOff && PlusForLife[x] is null).ToList();

        foreach (var userId in owed)
        {
            PlusForLife[userId] = grantedAt;
        }

        return Task.FromResult(Result.Success((long)owed.Count));
    }

    /// <summary>The same narrowing the Mongo query does, so a test of the pass is a test of what the pass would be handed.</summary>
    public Task<List<User>> GetTrialReminderCandidates(
        DateTime createdOnOrBefore,
        int belowStage,
        DateTime? createdOnOrAfter,
        int limit,
        CancellationToken ct)
    {
        CandidateSearches++;

        var candidates = Plus.Keys
            .Where(x => Created[x] <= createdOnOrBefore)
            .Where(x => (TrialReminderStage[x] ?? 0) < belowStage)
            .Where(x => PlusForLife[x] is null)
            .Where(x => createdOnOrAfter is null || Created[x] >= createdOnOrAfter)
            .OrderBy(x => Created[x])
            .Take(limit)
            .Select(Get)
            .ToList();

        return Task.FromResult(candidates);
    }

    /// <summary>The same compare-and-set the Mongo implementation does in one conditional write.</summary>
    public Task<bool> TryAdvanceTrialReminderStage(string userId, int toStage, CancellationToken ct)
    {
        if (!Plus.ContainsKey(userId) || (TrialReminderStage[userId] ?? 0) >= toStage)
        {
            return Task.FromResult(false);
        }

        TrialReminderStage[userId] = toStage;

        return Task.FromResult(true);
    }

    public Task EnsureIndexes(CancellationToken ct) => Task.CompletedTask;

    public Task<Result> SetDonations(string userId, UserDonations? donations, CancellationToken ct) => throw new NotSupportedException();
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
/// Where the moment Plus went live is kept. Starts empty, as a database does before Plus has ever
/// been live on it, and like the real one never replaces a launch that is already there.
/// </summary>
internal class FakePlusStateRepository : IPlusStateRepository
{
    public PlusLaunch? Launch { get; set; }

    public int Reads { get; private set; }

    public int Writes { get; private set; }

    /// <summary>Set to make every call fail, the way a database that cannot be reached does.</summary>
    public bool IsDown { get; set; }

    public FakePlusStateRepository()
    {
    }

    /// <summary>A database on which Plus went live at <paramref name="liveSince"/>.</summary>
    public FakePlusStateRepository(DateTime liveSince)
    {
        Launch = new PlusLaunch
        {
            Id = PlusLaunch.DocumentId,
            Created = liveSince,
            Updated = liveSince,
            LiveSince = liveSince,
        };
    }

    public Task<Maybe<PlusLaunch>> GetLaunch(CancellationToken ct)
    {
        if (IsDown)
        {
            throw new TimeoutException("The database cannot be reached");
        }

        Reads++;

        return Task.FromResult(Launch is not null ? Launch : Maybe<PlusLaunch>.None);
    }

    public Task<Result> InsertLaunchIfAbsent(PlusLaunch launch, CancellationToken ct)
    {
        if (IsDown)
        {
            throw new TimeoutException("The database cannot be reached");
        }

        Writes++;
        Launch ??= launch;

        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// The feed, recording what was written to it. Push has no transport in these tests, so this is
/// all a notice leaves behind.
/// </summary>
internal class FakePlusNotificationsRepository : INotificationsRepository
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

/// <summary>Preferences that say only what time zone someone is in. Nobody has any until a test gives them one.</summary>
internal class FakePlusUserPreferencesRepository : IUserPreferencesRepository
{
    public Dictionary<string, string> TimeZones { get; } = new();

    public Task<Maybe<UserPreferences>> GetById(string id, CancellationToken ct)
    {
        if (!TimeZones.TryGetValue(id, out var timeZone))
        {
            return Task.FromResult(Maybe<UserPreferences>.None);
        }

        return Task.FromResult<Maybe<UserPreferences>>(
            new UserPreferences
            {
                Id = id,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                Currency = null,
                TimeZone = timeZone,
            });
    }

    public Task<IList<UserPreferences>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserPreferences> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserPreferences updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>Never reached: push sending stops before looking anyone up when no transport is set.</summary>
internal class UnusedPlusPushSubscriptionsRepository : IPushSubscriptionsRepository
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
