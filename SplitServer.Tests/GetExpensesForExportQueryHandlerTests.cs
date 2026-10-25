using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Queries;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Tests;

/// <summary>
/// The rows behind an exported report. What these pin down is that the file is self-contained —
/// every id has become a name — and that it holds exactly what the chosen scope and window say,
/// with the window measured in the user's own days rather than in UTC.
/// </summary>
public class GetExpensesForExportQueryHandlerTests
{
    private const string UserId = "me";
    private const string MyMemberId = "member-me";
    private const string FriendUserId = "friend";
    private const string FriendMemberId = "member-friend";
    private const string GuestId = "guest-sam";
    private const string TripGroupId = "group-trip";

    private const string OtherGroupId = "group-flat";
    private const string MyOtherMemberId = "member-me-flat";

    private static readonly DateTime Jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Personal_scope_holds_only_the_users_own_expenses()
    {
        // The repository call behind "your expenses" also returns the group and quick-split ones
        // the user has a share in. Those have their own flags and must not leak in through this one.
        var handler = BuildHandler(
            personalExpenses: [PersonalExpense(Jan1, "EUR", 80m)],
            nonGroupExpenses: [NonGroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 30m)],
            groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m)]);

        var items = await Export(handler, Jan1, Jan1, BudgetScope.Personal);

        var item = Assert.Single(items);
        Assert.Equal(ExpenseResponseType.Personal, item.Type);
        Assert.Equal(80m, item.YourShare);
        Assert.Equal(80m, item.YouPaid);
        Assert.True(Assert.Single(item.PaidBy).IsYou);
        Assert.Empty(item.SplitWith);
    }

    [Fact]
    public async Task Every_flag_adds_its_own_kind()
    {
        var handler = BuildHandler(
            personalExpenses: [PersonalExpense(Jan1, "EUR", 80m)],
            nonGroupExpenses: [NonGroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 30m)],
            groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m)]);

        var all = BudgetScope.Personal | BudgetScope.NonGroup | BudgetScope.Group;

        var items = await Export(handler, Jan1, Jan1, all);

        Assert.Equal(3, items.Count);
        Assert.Contains(items, x => x.Type == ExpenseResponseType.Personal);
        Assert.Contains(items, x => x.Type == ExpenseResponseType.NonGroup);
        Assert.Contains(items, x => x.Type == ExpenseResponseType.Group);
    }

    [Fact]
    public async Task Group_scope_can_be_narrowed_to_chosen_groups()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 40m, groupId: OtherGroupId, myMemberId: MyOtherMemberId)
            ],
            groups: [TripGroup(), FlatGroup()]);

        var everything = await Export(handler, Jan1, Jan1, BudgetScope.Group);
        var flatOnly = await Export(handler, Jan1, Jan1, BudgetScope.Group, targetGroupIds: [OtherGroupId]);

        Assert.Equal(2, everything.Count);
        var item = Assert.Single(flatOnly);
        Assert.Equal("Flat", item.GroupName);
        Assert.Equal(40m, item.YourShare);
    }

    /// <summary>
    /// A file leaves the app, so nothing in it can be looked up later. Members are named by their
    /// account, guests by what the group calls them, and the user is flagged so the file can say
    /// "you" rather than echo their own username at them.
    /// </summary>
    [Fact]
    public async Task Everyone_is_named_rather_than_identified()
    {
        var expense = new SplitServer.Models.GroupExpense
        {
            Id = "e1",
            GroupId = TripGroupId,
            Occurred = Jan1,
            CreatorId = FriendUserId,
            Amount = 90m,
            Description = "Dinner",
            Currency = "EUR",
            Location = null,
            Labels = [],
            Payments = [new GroupPayment { MemberId = FriendMemberId, Amount = 90m }],
            Shares =
            [
                new GroupShare { MemberId = MyMemberId, Amount = 30m },
                new GroupShare { MemberId = FriendMemberId, Amount = 30m },
                new GroupShare { MemberId = GuestId, Amount = 30m }
            ],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };

        var handler = BuildHandler(groupExpenses: [expense]);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.Group));

        Assert.Equal("Trip", item.GroupName);

        var payer = Assert.Single(item.PaidBy);
        Assert.Equal("friend-name", payer.Name);
        Assert.False(payer.IsYou);
        Assert.Equal(90m, payer.Amount);

        Assert.Equal(["me-name", "friend-name", "Sam"], item.SplitWith.Select(x => x.Name));
        Assert.Equal([true, false, false], item.SplitWith.Select(x => x.IsYou));

        Assert.Equal(30m, item.YourShare);
        Assert.Equal(0m, item.YouPaid);
    }

    [Fact]
    public async Task A_deleted_account_still_gets_a_name()
    {
        var expense = new SplitServer.Models.NonGroupExpense
        {
            Id = "e1",
            Occurred = Jan1,
            CreatorId = UserId,
            Amount = 50m,
            Description = "Taxi",
            Currency = "EUR",
            Location = null,
            Labels = [],
            Payments = [new Payment { UserId = UserId, Amount = 50m }],
            Shares = [new Share { UserId = UserId, Amount = 25m }, new Share { UserId = "gone", Amount = 25m }],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };

        var handler = BuildHandler(nonGroupExpenses: [expense]);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.NonGroup));

        Assert.StartsWith("deleted-user-", item.SplitWith[1].Name);
        Assert.Equal(50m, item.YouPaid);
        Assert.Equal(25m, item.YourShare);
    }

    /// <summary>
    /// Fronting the whole bill for other people is still the user's expense to see in the file,
    /// even though none of it was theirs to bear.
    /// </summary>
    [Fact]
    public async Task Paying_for_others_counts_as_paid_but_not_as_a_share()
    {
        var expense = new SplitServer.Models.GroupExpense
        {
            Id = "e1",
            GroupId = TripGroupId,
            Occurred = Jan1,
            CreatorId = UserId,
            Amount = 60m,
            Description = "Tickets",
            Currency = "EUR",
            Location = null,
            Labels = [],
            Payments = [new GroupPayment { MemberId = MyMemberId, Amount = 60m }],
            Shares = [new GroupShare { MemberId = FriendMemberId, Amount = 60m }],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };

        var handler = BuildHandler(groupExpenses: [expense]);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.Group));

        Assert.Equal(60m, item.YouPaid);
        Assert.Equal(0m, item.YourShare);
    }

    /// <summary>
    /// Berlin is an hour ahead of UTC in January. An expense at half past midnight on the 1st,
    /// Berlin time, is still the 31st in UTC and would fall outside a window measured there.
    /// </summary>
    [Fact]
    public async Task The_window_is_whole_days_where_the_user_lives()
    {
        var justInside = new DateTime(2024, 12, 31, 23, 30, 0, DateTimeKind.Unspecified);
        var justOutside = new DateTime(2025, 1, 1, 23, 30, 0, DateTimeKind.Unspecified);

        var handler = BuildHandler(
            personalExpenses:
            [
                PersonalExpense(justInside, "EUR", 10m, id: "inside"),
                PersonalExpense(justOutside, "EUR", 20m, id: "outside")
            ],
            timeZoneId: "Europe/Berlin");

        var items = await Export(handler, Jan1, Jan1, BudgetScope.Personal);

        Assert.Equal("inside", Assert.Single(items).Id);
    }

    [Fact]
    public async Task Expenses_come_oldest_first()
    {
        var handler = BuildHandler(
            personalExpenses: [PersonalExpense(Jan1.AddDays(5), "EUR", 10m, id: "later")],
            groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 5m, id: "earlier")]);

        var items = await Export(handler, Jan1, Jan1.AddDays(10), BudgetScope.Personal | BudgetScope.Group);

        Assert.Equal(["earlier", "later"], items.Select(x => x.Id));
    }

    /// <summary>
    /// Rates are quoted against USD, and a dollar is worth half a euro in these fixtures, so a
    /// 100 USD share reads as 50 EUR in the user's own currency.
    /// </summary>
    [Fact]
    public async Task Shares_are_converted_at_the_rate_of_their_own_day()
    {
        var handler = BuildHandler(groupExpenses: [GroupExpense(Jan1, "USD", paidByMe: 100m, myShare: 100m)]);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.Group));

        Assert.Equal(50m, item.YourShareConverted);
        Assert.Equal(50m, item.YouPaidConverted);
    }

    /// <summary>
    /// A missing quote costs the converted column, not the whole report.
    /// </summary>
    [Fact]
    public async Task A_report_is_still_produced_when_no_rates_exist()
    {
        var handler = BuildHandler(
            groupExpenses: [GroupExpense(Jan1, "USD", paidByMe: 100m, myShare: 100m)],
            rates: []);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.Group));

        Assert.Equal(100m, item.YourShare);
        Assert.Null(item.YourShareConverted);
    }

    [Fact]
    public async Task Group_labels_are_named_by_the_group()
    {
        var expense = GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 10m) with { Labels = ["label-food", "label-unknown"] };

        var handler = BuildHandler(groupExpenses: [expense]);

        var item = Assert.Single(await Export(handler, Jan1, Jan1, BudgetScope.Group));

        Assert.Equal(["Food", "label-unknown"], item.Labels);
    }

    [Fact]
    public async Task Nothing_selected_is_refused()
    {
        var handler = BuildHandler(personalExpenses: [PersonalExpense(Jan1, "EUR", 10m)]);

        var result = await handler.Handle(Query(Jan1, Jan1, BudgetScope.None), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task An_end_before_the_start_is_refused()
    {
        var handler = BuildHandler(personalExpenses: [PersonalExpense(Jan1, "EUR", 10m)]);

        var result = await handler.Handle(Query(Jan1.AddDays(3), Jan1, BudgetScope.Personal), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private static async Task<List<ExportExpenseItem>> Export(
        GetExpensesForExportQueryHandler handler,
        DateTime startDate,
        DateTime endDate,
        BudgetScope scope,
        List<string>? targetGroupIds = null)
    {
        var result = await handler.Handle(Query(startDate, endDate, scope, targetGroupIds), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);

        return result.Value.Expenses;
    }

    private static GetExpensesForExportQuery Query(
        DateTime startDate,
        DateTime endDate,
        BudgetScope scope,
        List<string>? targetGroupIds = null)
    {
        return new GetExpensesForExportQuery
        {
            UserId = UserId,
            StartDate = startDate,
            EndDate = endDate,
            Scope = scope,
            TargetGroupIds = targetGroupIds ?? []
        };
    }

    private static Group TripGroup()
    {
        return new Group
        {
            Id = TripGroupId,
            OwnerId = FriendUserId,
            Name = "Trip",
            Currency = "EUR",
            IsArchived = false,
            Members =
            [
                new Member { Id = MyMemberId, UserId = UserId, Joined = DateTime.UnixEpoch },
                new Member { Id = FriendMemberId, UserId = FriendUserId, Joined = DateTime.UnixEpoch }
            ],
            Guests = [new Guest { Id = GuestId, Name = "Sam", Joined = DateTime.UnixEpoch }],
            Labels = [new Label { Id = "label-food", Text = "Food", Color = "#fff" }],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static Group FlatGroup()
    {
        return new Group
        {
            Id = OtherGroupId,
            OwnerId = UserId,
            Name = "Flat",
            Currency = "EUR",
            IsArchived = false,
            Members = [new Member { Id = MyOtherMemberId, UserId = UserId, Joined = DateTime.UnixEpoch }],
            Guests = [],
            Labels = [],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static GetExpensesForExportQueryHandler BuildHandler(
        IEnumerable<SplitServer.Models.GroupExpense>? groupExpenses = null,
        IEnumerable<SplitServer.Models.NonGroupExpense>? nonGroupExpenses = null,
        IEnumerable<SplitServer.Models.PersonalExpense>? personalExpenses = null,
        IEnumerable<Group>? groups = null,
        string timeZoneId = "UTC",
        IEnumerable<CurrencyExchangeRates>? rates = null)
    {
        return new GetExpensesForExportQueryHandler(
            new FakeUserPreferencesRepository(timeZoneId),
            new FakeUsersRepository(
            [
                User(UserId, "me-name"),
                User(FriendUserId, "friend-name")
            ]),
            new FakeGroupsRepository([.. groups ?? [TripGroup()]]),
            new FakeExpensesRepository(
                [.. groupExpenses ?? []],
                [.. nonGroupExpenses ?? []],
                [.. personalExpenses ?? []]),
            new FakeUserLabelsRepository(),
            new CurrencyExchangeRateService(null!, new FakeCurrencyExchangeRatesRepository([.. rates ?? [Rates("2000-01-01", eurPerUsd: 0.5m)]])),
            new ValidationService());
    }

    private static User User(string id, string username)
    {
        return new User
        {
            Id = id,
            Email = null,
            EmailVerified = false,
            HashedPassword = null,
            Username = username,
            GoogleId = null,
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static CurrencyExchangeRates Rates(string date, decimal eurPerUsd)
    {
        return new CurrencyExchangeRates
        {
            Id = date,
            Base = "USD",
            Date = DateOnly.Parse(date),
            Rates = new Dictionary<string, decimal> { ["USD"] = 1m, ["EUR"] = eurPerUsd },
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static SplitServer.Models.GroupExpense GroupExpense(
        DateTime occurred,
        string currency,
        decimal paidByMe,
        decimal myShare,
        string groupId = TripGroupId,
        string myMemberId = MyMemberId,
        string? id = null)
    {
        return new SplitServer.Models.GroupExpense
        {
            Id = id ?? Guid.NewGuid().ToString(),
            GroupId = groupId,
            Occurred = occurred,
            CreatorId = UserId,
            Amount = Math.Max(paidByMe, myShare),
            Description = "expense",
            Currency = currency,
            Location = null,
            Labels = [],
            Payments = paidByMe > 0 ? [new GroupPayment { MemberId = myMemberId, Amount = paidByMe }] : [],
            Shares = [new GroupShare { MemberId = myMemberId, Amount = myShare }],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static SplitServer.Models.NonGroupExpense NonGroupExpense(
        DateTime occurred,
        string currency,
        decimal paidByMe,
        decimal myShare)
    {
        return new SplitServer.Models.NonGroupExpense
        {
            Id = Guid.NewGuid().ToString(),
            Occurred = occurred,
            CreatorId = UserId,
            Amount = Math.Max(paidByMe, myShare),
            Description = "expense",
            Currency = currency,
            Location = null,
            Labels = [],
            Payments = paidByMe > 0 ? [new Payment { UserId = UserId, Amount = paidByMe }] : [],
            Shares = [new Share { UserId = UserId, Amount = myShare }],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static SplitServer.Models.PersonalExpense PersonalExpense(DateTime occurred, string currency, decimal amount, string? id = null)
    {
        return new SplitServer.Models.PersonalExpense
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Occurred = occurred,
            CreatorId = UserId,
            Amount = amount,
            Description = "expense",
            Currency = currency,
            Location = null,
            Labels = [],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }
}

file class FakeUserPreferencesRepository : IUserPreferencesRepository
{
    private readonly string _timeZoneId;

    public FakeUserPreferencesRepository(string timeZoneId) => _timeZoneId = timeZoneId;

    public Task<Maybe<UserPreferences>> GetById(string id, CancellationToken ct)
    {
        return Task.FromResult<Maybe<UserPreferences>>(
            new UserPreferences
            {
                Id = id,
                Currency = "EUR",
                TimeZone = _timeZoneId,
                Created = DateTime.UnixEpoch,
                Updated = DateTime.UnixEpoch
            });
    }

    public Task<IList<UserPreferences>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserPreferences> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserPreferences entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserPreferences updatedEntity, CancellationToken ct) => throw new NotSupportedException();
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

    public Task<IList<User>> GetByIds(IList<string> ids, CancellationToken ct)
    {
        return Task.FromResult<IList<User>>(_users.Where(x => ids.Contains(x.Id)).ToList());
    }

    public Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> AnyWithUsername(string username, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<User> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(User entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(User updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeUserLabelsRepository : IUserLabelsRepository
{
    public Task<List<UserLabel>> GetByUserId(string userId, CancellationToken ct) => Task.FromResult(new List<UserLabel>());

    public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<UserLabel>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<UserLabel>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserLabel entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<UserLabel> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserLabel entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserLabel updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// Applies the same window the Mongo queries do, so what is under test is the handler's choice of
/// which expenses to ask for and how to describe them.
/// </summary>
file class FakeExpensesRepository : IExpensesRepository
{
    private readonly List<SplitServer.Models.GroupExpense> _groupExpenses;
    private readonly List<SplitServer.Models.NonGroupExpense> _nonGroupExpenses;
    private readonly List<SplitServer.Models.PersonalExpense> _personalExpenses;

    public FakeExpensesRepository(
        List<SplitServer.Models.GroupExpense> groupExpenses,
        List<SplitServer.Models.NonGroupExpense> nonGroupExpenses,
        List<SplitServer.Models.PersonalExpense> personalExpenses)
    {
        _groupExpenses = groupExpenses;
        _nonGroupExpenses = nonGroupExpenses;
        _personalExpenses = personalExpenses;
    }

    public Task<List<SplitServer.Models.GroupExpense>> GetGroupExpensesByMemberIds(
        List<string> memberIds,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var matches = _groupExpenses
            .Where(x => x.Shares.Any(s => memberIds.Contains(s.MemberId)) || x.Payments.Any(p => memberIds.Contains(p.MemberId)));

        return Task.FromResult(InWindow(matches, startDate, endDate).ToList());
    }

    public Task<List<SplitServer.Models.NonGroupExpense>> GetNonGroupExpensesByUserId(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var matches = _nonGroupExpenses
            .Where(x => x.Shares.Any(s => s.UserId == userId) || x.Payments.Any(p => p.UserId == userId));

        return Task.FromResult(InWindow(matches, startDate, endDate).ToList());
    }

    public Task<List<Expense>> GetPersonalExpensesByUserId(
        string userId,
        List<string> memberIds,
        CancellationToken ct,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var matches = _personalExpenses
            .Where(x => x.CreatorId == userId)
            .Cast<Expense>()
            .Concat(_nonGroupExpenses.Where(x => x.Shares.Any(s => s.UserId == userId)))
            .Concat(_groupExpenses.Where(x => x.Shares.Any(s => memberIds.Contains(s.MemberId))));

        return Task.FromResult(InWindow(matches, startDate, endDate).ToList());
    }

    public Task<long> CountByCreatorId(string creatorId, int limit, CancellationToken ct) => Task.FromResult(0L);

    private static IEnumerable<T> InWindow<T>(IEnumerable<T> expenses, DateTime? startDate, DateTime? endDate)
        where T : Expense
    {
        if (startDate.HasValue) expenses = expenses.Where(x => x.Occurred >= startDate.Value);
        if (endDate.HasValue) expenses = expenses.Where(x => x.Occurred <= endDate.Value);

        return expenses.OrderBy(x => x.Occurred);
    }

    public Task<List<SplitServer.Models.GroupExpense>> GetByGroupId(string groupId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.GroupExpense>> GetGroupExpensesByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> ClearRecurringExpenseId(string recurringExpenseId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> UserLabelInUse(string labelText, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.GroupExpense>> Search(string groupId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.NonGroupExpense>> GetNonGroupByUserId(string userId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> GetPersonalByUserId(string userId, List<string> memberIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> SearchPersonalByUserId(string userId, List<string> memberIds, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? labels, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<SplitServer.Models.NonGroupExpense>> SearchNonGroup(string userId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
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

/// <summary>
/// Membership is decided by the Members array alone, the way the Mongo filter decides it.
/// </summary>
file class FakeGroupsRepository : IGroupsRepository
{
    private readonly List<Group> _groups;

    public FakeGroupsRepository(List<Group> groups) => _groups = groups;

    public Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct)
    {
        return Task.FromResult(_groups.Where(g => g.Members.Any(m => m.UserId == userId)).ToList());
    }

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

file class FakeCurrencyExchangeRatesRepository : ICurrencyExchangeRatesRepository
{
    private readonly List<CurrencyExchangeRates> _rates;

    public FakeCurrencyExchangeRatesRepository(List<CurrencyExchangeRates> rates) => _rates = rates;

    public Task<Maybe<CurrencyExchangeRates>> GetLatest(CancellationToken ct)
    {
        var latest = _rates.OrderByDescending(x => x.Date).FirstOrDefault();

        return Task.FromResult(latest is not null ? latest : Maybe<CurrencyExchangeRates>.None);
    }

    public Task<List<CurrencyExchangeRates>> GetByDates(IReadOnlyCollection<DateOnly> dates, CancellationToken ct)
    {
        return Task.FromResult(_rates.Where(x => dates.Contains(x.Date)).OrderBy(x => x.Date).ToList());
    }

    public Task<Maybe<CurrencyExchangeRates>> GetLatestOnOrBefore(DateOnly date, CancellationToken ct)
    {
        var match = _rates.Where(x => x.Date <= date).OrderByDescending(x => x.Date).FirstOrDefault();

        return Task.FromResult(match is not null ? match : Maybe<CurrencyExchangeRates>.None);
    }

    public Task<Maybe<CurrencyExchangeRates>> GetByDate(DateOnly date, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<CurrencyExchangeRates>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<CurrencyExchangeRates>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(CurrencyExchangeRates entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<CurrencyExchangeRates> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(CurrencyExchangeRates entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(CurrencyExchangeRates updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}
