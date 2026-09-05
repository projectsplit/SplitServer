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
/// The lent and borrowed series behind the analytics chart. What these cover is that the two are
/// decided per expense: a day where the user fronted money for one expense and was covered for
/// another has to report both, not the net of the two. Netting first collapses the smaller side to
/// zero and makes the answer depend on how wide a bucket the chart happens to be drawing.
/// </summary>
public class GetSpendingsChartQueryHandlerTests
{
    private const string UserId = "me";
    private const string MyMemberId = "member-me";
    private const string FriendMemberId = "member-friend";
    private const string FriendUserId = "friend";

    private static readonly DateTime Jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Lending_and_borrowing_on_the_same_day_are_both_reported()
    {
        // Fronted 75 on the dinner, covered for 20 on the taxi. Netting would say "lent 55".
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 20m)
            ]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(75m, items[0].LentAmount);
        Assert.Equal(20m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// The other two charts read the share and payment sums, so they must come through untouched.
    /// </summary>
    [Fact]
    public async Task Share_and_payment_sums_are_unchanged()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 20m)
            ]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(45m, items[0].ShareAmount);
        Assert.Equal(100m, items[0].PaymentAmount);
    }

    /// <summary>
    /// The same expenses seen through the monthly view (the Annually cycle) and the daily view (the
    /// Monthly cycle). A netted total shrinks as the bucket widens; a per-expense one does not.
    /// </summary>
    [Fact]
    public async Task Totals_do_not_change_with_granularity()
    {
        SplitServer.Models.GroupExpense[] expenses =
        [
            GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
            GroupExpense(Jan1.AddDays(5), "EUR", paidByMe: 0m, myShare: 20m),
            GroupExpense(Jan1.AddDays(12), "EUR", paidByMe: 60m, myShare: 30m),
            GroupExpense(Jan1.AddDays(12), "EUR", paidByMe: 0m, myShare: 45m)
        ];

        var daily = await Chart(BuildHandler(groupExpenses: expenses), Jan1, Jan1.AddDays(30), "daily");
        var monthly = await Chart(BuildHandler(groupExpenses: expenses), Jan1, Jan1.AddDays(30), "monthly");

        Assert.Equal(105m, daily[^1].AccumulativeLentAmount);
        Assert.Equal(65m, daily[^1].AccumulativeBorrowedAmount);

        Assert.Equal(daily[^1].AccumulativeLentAmount, monthly[^1].AccumulativeLentAmount);
        Assert.Equal(daily[^1].AccumulativeBorrowedAmount, monthly[^1].AccumulativeBorrowedAmount);
    }

    [Fact]
    public async Task Non_group_expenses_are_counted_too()
    {
        var handler = BuildHandler(
            groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m)],
            nonGroupExpenses: [NonGroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 30m)]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(75m, items[0].LentAmount);
        Assert.Equal(30m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// A personal expense is the user's own money on themselves. It belongs in the spending total
    /// but is neither lending nor borrowing.
    /// </summary>
    [Fact]
    public async Task Personal_expenses_are_spending_but_never_lending_or_borrowing()
    {
        var handler = BuildHandler(personalExpenses: [PersonalExpense(Jan1, "EUR", 80m)]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(0m, items[0].LentAmount);
        Assert.Equal(0m, items[0].BorrowedAmount);
        Assert.Equal(80m, items[0].ShareAmount);
        Assert.Equal(80m, items[0].AccumulativeShareAmount);
    }

    /// <summary>
    /// The accumulative figures are what the charts actually plot, and a consumer is entitled to
    /// assume they are the running sum of the per-bucket ones. Personal expenses used to be added
    /// to the running share total without ever appearing in the per-bucket one.
    /// </summary>
    [Fact]
    public async Task Accumulative_figures_are_the_running_sum_of_the_per_bucket_ones()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1.AddDays(2), "EUR", paidByMe: 0m, myShare: 20m)
            ],
            nonGroupExpenses: [NonGroupExpense(Jan1.AddDays(3), "EUR", paidByMe: 50m, myShare: 10m)],
            personalExpenses: [PersonalExpense(Jan1.AddDays(1), "EUR", 80m)]);

        var items = await Chart(handler, Jan1, Jan1.AddDays(5), "daily");

        Assert.Equal(items.Sum(x => x.ShareAmount), items[^1].AccumulativeShareAmount);
        Assert.Equal(items.Sum(x => x.PaymentAmount), items[^1].AccumulativePaymentAmount);
        Assert.Equal(items.Sum(x => x.LentAmount), items[^1].AccumulativeLentAmount);
        Assert.Equal(items.Sum(x => x.BorrowedAmount), items[^1].AccumulativeBorrowedAmount);
    }

    /// <summary>
    /// Each expense is converted before it is classified, so a lent amount and a borrowed amount in
    /// different currencies cannot cancel each other out at the wrong rate.
    /// </summary>
    [Fact]
    public async Task Foreign_currency_expenses_are_converted_before_being_classified()
    {
        // Rates are quoted against USD, and USD is worth half a EUR here, so 100 USD is 50 EUR.
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "USD", paidByMe: 100m, myShare: 20m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 10m)
            ]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(40m, items[0].LentAmount);
        Assert.Equal(10m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// Running totals only ever climb, which is what lets the chart read the last item as the
    /// period total and what makes the filled band between the two lines meaningful.
    /// </summary>
    [Fact]
    public async Task Running_totals_never_go_backwards()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1.AddDays(3), "EUR", paidByMe: 0m, myShare: 40m),
                GroupExpense(Jan1.AddDays(6), "EUR", paidByMe: 90m, myShare: 30m)
            ]);

        var items = await Chart(handler, Jan1, Jan1.AddDays(9), "daily");

        Assert.Equal(items.Select(x => x.AccumulativeLentAmount).Order(), items.Select(x => x.AccumulativeLentAmount));
        Assert.Equal(items.Select(x => x.AccumulativeBorrowedAmount).Order(), items.Select(x => x.AccumulativeBorrowedAmount));
    }

    /// <summary>
    /// Netting is still the right answer inside a single expense: paying 100 towards a 25 share is
    /// one act of lending 75, not lending 100 and borrowing 25.
    /// </summary>
    [Fact]
    public async Task A_single_expense_nets_its_own_share_against_its_own_payment()
    {
        var handler = BuildHandler(groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m)]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(75m, items[0].LentAmount);
        Assert.Equal(0m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// Leaving a group stops its expenses counting towards the person who left. The expenses stay
    /// exactly where they are and the group carries on editing them against the guest that now
    /// holds the id — but that guest belongs to the group, so none of it is the departed user's any
    /// more, and they cannot see the group to check what it says.
    /// </summary>
    [Fact]
    public async Task A_group_the_user_left_counts_for_nothing()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 20m)
            ],
            group: GroupTheUserLeft());

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(0m, items[0].ShareAmount);
        Assert.Equal(0m, items[0].LentAmount);
        Assert.Equal(0m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// Rejoining means being invited onto the guest slot, which hands the original member id back.
    /// Every expense and transfer already points at that id, so the whole history returns with it
    /// and nothing has to be copied or restored.
    /// </summary>
    [Fact]
    public async Task Reclaiming_the_guest_slot_brings_the_whole_history_back()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m),
                GroupExpense(Jan1, "EUR", paidByMe: 0m, myShare: 20m)
            ],
            group: ActiveGroup());

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(45m, items[0].ShareAmount);
        Assert.Equal(75m, items[0].LentAmount);
        Assert.Equal(20m, items[0].BorrowedAmount);
    }

    /// <summary>
    /// A guest is a group's own participant and never resolves to an account, whether it stands in
    /// for someone who left or for someone who never had one.
    /// </summary>
    [Fact]
    public async Task A_guests_spending_is_not_claimed_by_anyone()
    {
        var handler = BuildHandler(
            groupExpenses: [GroupExpense(Jan1, "EUR", paidByMe: 100m, myShare: 25m)],
            group: GroupWithAPlainGuest());

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(0m, items[0].LentAmount);
        Assert.Equal(0m, items[0].BorrowedAmount);
        Assert.Equal(0m, items[0].ShareAmount);
    }

    /// <summary>
    /// Berlin puts its clocks back on 26 October 2025, making that local day 25 hours long. The
    /// bucket used to run for a fixed 24 hours from the start of the day in UTC while the next one
    /// began an hour later still, so the last hour belonged to no bucket at all and the expenses in
    /// it were dropped from the totals outright.
    /// </summary>
    [Fact]
    public async Task An_expense_in_the_extra_hour_of_a_25_hour_day_is_still_counted()
    {
        // 23:30 local on the 26th, inside the hour the old fixed-length bucket ran past.
        var occurred = new DateTime(2025, 10, 26, 22, 30, 0, DateTimeKind.Unspecified);

        var handler = BuildHandler(
            groupExpenses: [GroupExpense(occurred, "EUR", paidByMe: 100m, myShare: 40m)],
            timeZoneId: "Europe/Berlin");

        var items = await Chart(handler, new DateTime(2025, 10, 26), new DateTime(2025, 10, 26), "daily");

        Assert.Equal(40m, items.Sum(x => x.ShareAmount));
        Assert.Equal(60m, items.Sum(x => x.LentAmount));
    }

    /// <summary>
    /// The other side of the same bug. Berlin loses an hour on 30 March 2025, so a fixed 24-hour
    /// bucket overran into the next day, which began its own bucket an hour early — and anything in
    /// the overlap was counted in both.
    /// </summary>
    [Fact]
    public async Task An_expense_either_side_of_a_23_hour_day_is_counted_once()
    {
        // 00:30 local on the 31st, inside the hour the old bucket for the 30th also claimed.
        var occurred = new DateTime(2025, 3, 30, 22, 30, 0, DateTimeKind.Unspecified);

        var handler = BuildHandler(
            groupExpenses: [GroupExpense(occurred, "EUR", paidByMe: 100m, myShare: 40m)],
            timeZoneId: "Europe/Berlin");

        var items = await Chart(handler, new DateTime(2025, 3, 29), new DateTime(2025, 3, 31), "daily");

        Assert.Equal(40m, items.Sum(x => x.ShareAmount));
        Assert.Equal(40m, items[^1].AccumulativeShareAmount);
    }

    /// <summary>
    /// Two identical dollar expenses months apart are not worth the same in euros. Converting both
    /// at today's rate rewrites what last year's spending cost.
    /// </summary>
    [Fact]
    public async Task Each_expense_is_converted_at_the_rate_of_its_own_day()
    {
        var handler = BuildHandler(
            groupExpenses:
            [
                GroupExpense(Jan1, "USD", paidByMe: 0m, myShare: 100m),
                GroupExpense(Jan1.AddDays(10), "USD", paidByMe: 0m, myShare: 100m)
            ],
            rates:
            [
                Rates("2025-01-01", eurPerUsd: 0.5m),
                Rates("2025-01-11", eurPerUsd: 0.25m)
            ]);

        var items = await Chart(handler, Jan1, Jan1.AddDays(20), "daily");

        // 100 USD at 0.5 is 50 EUR; the same 100 USD at 0.25 is 25 EUR.
        Assert.Equal(50m, items[0].BorrowedAmount);
        Assert.Equal(25m, items[10].BorrowedAmount);
        Assert.Equal(75m, items[^1].AccumulativeBorrowedAmount);
    }

    /// <summary>
    /// The collection job stores one day at a time and can miss a run. A day with no quote of its
    /// own takes the most recent one before it, not the newest one on record.
    /// </summary>
    [Fact]
    public async Task A_day_with_no_quote_uses_the_most_recent_earlier_one()
    {
        var handler = BuildHandler(
            groupExpenses: [GroupExpense(Jan1.AddDays(5), "USD", paidByMe: 0m, myShare: 100m)],
            rates:
            [
                Rates("2025-01-01", eurPerUsd: 0.5m),
                Rates("2025-01-20", eurPerUsd: 0.25m)
            ]);

        var items = await Chart(handler, Jan1, Jan1.AddDays(20), "daily");

        // The 6th has no quote, so it carries the 1st forward rather than reaching for the 20th.
        Assert.Equal(50m, items[5].BorrowedAmount);
    }

    /// <summary>
    /// An expense older than every quote on record has nothing to carry forward, so the newest
    /// quote is the best available guess — the behaviour everything had before this change.
    /// </summary>
    [Fact]
    public async Task An_expense_older_than_every_quote_falls_back_to_the_newest()
    {
        var handler = BuildHandler(
            groupExpenses: [GroupExpense(Jan1, "USD", paidByMe: 0m, myShare: 100m)],
            rates: [Rates("2025-06-01", eurPerUsd: 0.25m)]);

        var items = await Chart(handler, Jan1, Jan1, "daily");

        Assert.Equal(25m, items[0].BorrowedAmount);
    }

    private static async Task<List<GetSpendingsChartResponseItem>> Chart(
        GetSpendingsChartQueryHandler handler,
        DateTime startDate,
        DateTime endDate,
        string granularity)
    {
        var result = await handler.Handle(
            new GetSpendingsChartQuery
            {
                UserId = UserId,
                Currency = "EUR",
                Granularity = granularity,
                StartDate = startDate,
                EndDate = endDate
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : string.Empty);

        return result.Value.Items;
    }

    /// <summary>
    /// The user is still in the group.
    /// </summary>
    private static Group ActiveGroup()
    {
        return BuildGroup(
            members:
            [
                new Member { Id = MyMemberId, UserId = UserId, Joined = DateTime.UnixEpoch },
                new Member { Id = FriendMemberId, UserId = FriendUserId, Joined = DateTime.UnixEpoch }
            ],
            guests: []);
    }

    /// <summary>
    /// What leaving produces: the member row is gone and a guest holds the same id, with nothing
    /// tying it back to the account it came from.
    /// </summary>
    private static Group GroupTheUserLeft()
    {
        return BuildGroup(
            members: [new Member { Id = FriendMemberId, UserId = FriendUserId, Joined = DateTime.UnixEpoch }],
            guests: [new Guest { Id = MyMemberId, Name = "me-guest", Joined = DateTime.UnixEpoch }]);
    }

    /// <summary>
    /// A guest the group invented for someone who never had an account. Indistinguishable, by
    /// design, from the one leaving produces.
    /// </summary>
    private static Group GroupWithAPlainGuest()
    {
        return BuildGroup(
            members: [new Member { Id = FriendMemberId, UserId = FriendUserId, Joined = DateTime.UnixEpoch }],
            guests: [new Guest { Id = MyMemberId, Name = "someone", Joined = DateTime.UnixEpoch }]);
    }

    private static Group BuildGroup(List<Member> members, List<Guest> guests)
    {
        return new Group
        {
            Id = "group-1",
            OwnerId = FriendUserId,
            Name = "group",
            Currency = "EUR",
            IsArchived = false,
            Members = members,
            Guests = guests,
            Labels = [],
            Created = DateTime.UnixEpoch,
            Updated = DateTime.UnixEpoch
        };
    }

    private static GetSpendingsChartQueryHandler BuildHandler(
        IEnumerable<SplitServer.Models.GroupExpense>? groupExpenses = null,
        IEnumerable<SplitServer.Models.NonGroupExpense>? nonGroupExpenses = null,
        IEnumerable<SplitServer.Models.PersonalExpense>? personalExpenses = null,
        Group? group = null,
        string timeZoneId = "UTC",
        IEnumerable<CurrencyExchangeRates>? rates = null)
    {
        group ??= ActiveGroup();

        return new GetSpendingsChartQueryHandler(
            new FakeUserPreferencesRepository(timeZoneId),
            new FakeExpensesRepository(
                [.. groupExpenses ?? []],
                [.. nonGroupExpenses ?? []],
                [.. personalExpenses ?? []]),
            new FakeGroupsRepository(group),
            new CurrencyExchangeRateService(null!, new FakeCurrencyExchangeRatesRepository([.. rates ?? [Rates("2000-01-01", eurPerUsd: 0.5m)]])),
            new ValidationService());
    }

    /// <summary>
    /// Quotes are against USD the way the real ones are, so a EUR value of 0.5 makes a dollar worth
    /// half a euro.
    /// </summary>
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
        decimal myShare)
    {
        return new SplitServer.Models.GroupExpense
        {
            Id = Guid.NewGuid().ToString(),
            GroupId = "group-1",
            Occurred = occurred,
            CreatorId = UserId,
            Description = "expense",
            Currency = currency,
            Location = null,
            Labels = [],
            Payments = paidByMe > 0 ? [new GroupPayment { MemberId = MyMemberId, Amount = paidByMe }] : [],
            Shares = [new GroupShare { MemberId = MyMemberId, Amount = myShare }],
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

    private static SplitServer.Models.PersonalExpense PersonalExpense(DateTime occurred, string currency, decimal amount)
    {
        return new SplitServer.Models.PersonalExpense
        {
            Id = Guid.NewGuid().ToString(),
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

/// <summary>
/// Applies the same date window the Mongo query does, so the handler's own bucketing is what is
/// under test rather than the repository's filtering.
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
    public Task<Maybe<Expense>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<Expense>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(Expense entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<Expense> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Expense entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Expense updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// Matches the Mongo filter: membership is decided by the Members array alone, so a group the user
/// has left is simply not theirs.
/// </summary>
file class FakeGroupsRepository : IGroupsRepository
{
    private readonly Group _group;

    public FakeGroupsRepository(Group group) => _group = group;

    public Task<List<Group>> GetAllByUserId(string userId, CancellationToken ct)
    {
        return Task.FromResult(_group.Members.Any(x => x.UserId == userId) ? [_group] : new List<Group>());
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

/// <summary>
/// Serves quotes the way the collection does — one document per day, with days the job never ran
/// simply absent — so the handler's own choice of which quote applies is what is under test.
/// </summary>
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
