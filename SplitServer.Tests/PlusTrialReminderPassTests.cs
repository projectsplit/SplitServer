using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using SplitServer.Commands;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The hourly pass telling accounts that their free Plus days are ending. The decision for one
/// account is pinned elsewhere; these are about the pass as a whole — who it goes looking for, who
/// it leaves alone, and that nobody is told the same thing twice however often it runs.
/// </summary>
public class PlusTrialReminderPassTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    /// <summary>Long enough ago that it never limits anyone's free days unless a test moves it.</summary>
    private static readonly DateTime LongLive = Now.AddDays(-300);

    /// <summary>Further back than any account in these tests, so the cut-off rule only applies where a test says so.</summary>
    private static readonly DateTime OldCutOff = Now.AddDays(-250);

    private readonly FakePlay _play = new();
    private readonly FakePlusSubscriptionsRepository _subscriptions = new();
    private readonly FakePlusUsersRepository _users = new();
    private readonly FakePlusNotificationsRepository _notifications = new();
    private readonly FakePlusUserPreferencesRepository _preferences = new();

    private ProcessPlusSubscriptionsCommandHandler CreateHandler(
        bool enabled = true,
        DateTime? liveSince = null,
        int trialDays = 30,
        int reminderDays = 7,
        int finalReminderDays = 1)
    {
        var catalog = new PlusCatalog(
            Options.Create(
                new PlusSettings
                {
                    Enabled = enabled,
                    TrialDays = trialDays,
                    LifetimeBefore = OldCutOff,
                    TrialReminderDays = reminderDays,
                    TrialFinalReminderDays = finalReminderDays,
                }));

        // No transport is configured, so push is a logged no-op and the feed is all that is written.
        var push = new PushNotificationService(
            Options.Create(new PushNotificationsSettings()),
            new UnusedPlusPushSubscriptionsRepository(),
            _preferences);

        return new ProcessPlusSubscriptionsCommandHandler(
            _play,
            catalog,
            new PlusRecorder(_play, catalog, _subscriptions, _users),
            new PlusAccessService(_play, catalog, new FakePlusStateRepository(liveSince ?? LongLive)),
            _subscriptions,
            _users,
            _preferences,
            new NotificationService(_notifications, push),
            new LockService());
    }

    private static async Task<ProcessPlusSubscriptionsResponse> Run(ProcessPlusSubscriptionsCommandHandler handler)
    {
        var result = await handler.Handle(new ProcessPlusSubscriptionsCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    /// <summary>An account whose free days began this long ago.</summary>
    private void Account(string userId, double daysAgo, DateTime? plusForLife = null, int? stage = null) =>
        _users.Add(userId, Now.AddDays(-daysAgo), plusForLife, stage);

    private const string WhatChanges =
        "After that, budgeting, analytics, search, places and payments from notifications need a subscription. " +
        "The rest of Buqs stays free.";

    // ---- The first notice -------------------------------------------------------------------

    [Fact]
    public async Task An_account_a_week_from_the_end_of_its_free_days_is_told()
    {
        Account("user-1", daysAgo: 24);

        var pass = await Run(CreateHandler());

        Assert.Equal(1, pass.TrialReminded);

        var notification = Assert.Single(_notifications.Inserted);
        Assert.Equal("user-1", notification.UserId);
        Assert.StartsWith("Your free Plus days end on ", notification.Title);
        Assert.Equal($"You have 6 days left of Buqs Plus. {WhatChanges}", notification.Body);
        Assert.Equal("/?plus=1", notification.Url);
        Assert.Equal(1, _users.TrialReminderStage["user-1"]);
    }

    [Fact]
    public async Task An_account_is_told_once_however_many_passes_see_it()
    {
        Account("user-1", daysAgo: 24);

        await Run(CreateHandler());
        var second = await Run(CreateHandler());
        var third = await Run(CreateHandler());

        Assert.Equal(0, second.TrialReminded);
        Assert.Equal(0, third.TrialReminded);
        Assert.Single(_notifications.Inserted);
    }

    /// <summary>
    /// The search is narrowed by when the account was made, so one with weeks of free days left is
    /// never read at all, let alone told anything.
    /// </summary>
    [Fact]
    public async Task An_account_with_plenty_of_free_days_left_is_left_alone()
    {
        Account("user-1", daysAgo: 10);

        var pass = await Run(CreateHandler());

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Null(_users.TrialReminderStage["user-1"]);
    }

    // ---- The last notice --------------------------------------------------------------------

    [Fact]
    public async Task An_account_a_day_from_the_end_is_told_for_the_last_time()
    {
        Account("user-1", daysAgo: 29.5, stage: 1);

        var pass = await Run(CreateHandler());

        Assert.Equal(1, pass.TrialReminded);

        var notification = Assert.Single(_notifications.Inserted);
        Assert.StartsWith("Your free Plus days end ", notification.Title);
        Assert.Equal(WhatChanges, notification.Body);
        Assert.Equal("/?plus=1", notification.Url);
        Assert.Equal(2, _users.TrialReminderStage["user-1"]);
    }

    /// <summary>
    /// The worker was down, or the account only just came into range. It gets the one notice that
    /// is still true, and not the first as well a moment later.
    /// </summary>
    [Fact]
    public async Task An_account_that_missed_the_first_notice_gets_the_last_one_only()
    {
        Account("user-1", daysAgo: 29.5);

        var pass = await Run(CreateHandler());
        var again = await Run(CreateHandler());

        Assert.Equal(1, pass.TrialReminded);
        Assert.Equal(0, again.TrialReminded);
        Assert.Equal(WhatChanges, Assert.Single(_notifications.Inserted).Body);
        Assert.Equal(2, _users.TrialReminderStage["user-1"]);
    }

    [Fact]
    public async Task Both_notices_reach_an_account_over_its_last_week_and_no_more_after_that()
    {
        Account("user-1", daysAgo: 24);
        await Run(CreateHandler());

        // Five and a half days on, the same account is inside the final day.
        _users.Created["user-1"] = Now.AddDays(-29.5);
        await Run(CreateHandler());
        await Run(CreateHandler());

        Assert.Equal(2, _notifications.Inserted.Count);
        Assert.StartsWith("You have 6 days left", _notifications.Inserted[0].Body);
        Assert.Equal(WhatChanges, _notifications.Inserted[1].Body);
    }

    // ---- Who is never told ------------------------------------------------------------------

    /// <summary>
    /// A warning after the fact is not a warning. The account is set aside without a word, so that
    /// it is not fetched again on every pass for the rest of its life.
    /// </summary>
    [Fact]
    public async Task An_account_whose_free_days_are_already_over_is_set_aside_without_being_told()
    {
        Account("user-1", daysAgo: 45);

        var pass = await Run(CreateHandler());

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Equal(2, _users.TrialReminderStage["user-1"]);
    }

    [Fact]
    public async Task Someone_who_has_subscribed_is_not_told_their_free_days_are_ending()
    {
        Account("user-1", daysAgo: 24);

        _users.Plus["user-1"] = new UserPlus
        {
            Plan = PlusPlan.Annual,
            ExpiresAt = Now.AddDays(300),
            AutoRenewing = true,
            State = "SUBSCRIPTION_STATE_ACTIVE",
            SubscriptionId = "token-1",
            RenewalPrice = 19.99m,
            RenewalCurrency = "GBP",
            Since = Now.AddDays(-5),
        };

        var pass = await Run(CreateHandler());

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Equal(2, _users.TrialReminderStage["user-1"]);
    }

    [Fact]
    public async Task Someone_granted_Plus_for_life_is_never_told()
    {
        Account("user-1", daysAgo: 24, plusForLife: Now.AddDays(-1));
        Account("user-2", daysAgo: 29.5, plusForLife: Now.AddDays(-1));

        var pass = await Run(CreateHandler());

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
    }

    [Fact]
    public async Task An_account_from_before_the_cut_off_is_never_told()
    {
        // Older than the cut-off, so it has Plus for life whether or not the grant has reached it.
        _users.Add("founder", OldCutOff.AddDays(-20));

        var pass = await Run(CreateHandler());

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Null(_users.TrialReminderStage["founder"]);
    }

    // ---- When Plus went live ----------------------------------------------------------------

    /// <summary>
    /// An account older than Plus starts its free days when Plus went live. A week after that it
    /// has three weeks left, however long ago it was made, and nobody is even looked for.
    /// </summary>
    [Fact]
    public async Task Nobody_is_told_anything_in_the_first_weeks_after_Plus_goes_live()
    {
        Account("user-1", daysAgo: 200);
        Account("user-2", daysAgo: 29.5);

        var pass = await Run(CreateHandler(liveSince: Now.AddDays(-7)));

        Assert.Equal(0, pass.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Equal(0, _users.CandidateSearches);
        Assert.Null(_users.TrialReminderStage["user-1"]);
        Assert.Null(_users.TrialReminderStage["user-2"]);
    }

    [Fact]
    public async Task An_account_older_than_Plus_is_told_a_week_before_its_free_days_from_the_launch_run_out()
    {
        Account("user-1", daysAgo: 200);

        var pass = await Run(CreateHandler(liveSince: Now.AddDays(-25)));

        Assert.Equal(1, pass.TrialReminded);
        Assert.Equal($"You have 5 days left of Buqs Plus. {WhatChanges}", Assert.Single(_notifications.Inserted).Body);
    }

    [Fact]
    public async Task Nobody_is_looked_for_while_Plus_is_not_live()
    {
        Account("user-1", daysAgo: 24);
        Account("user-2", daysAgo: 29.5);

        var switchedOff = await Run(CreateHandler(enabled: false));

        _play.IsConfigured = false;
        var noPlay = await Run(CreateHandler());

        Assert.Equal(0, switchedOff.TrialReminded);
        Assert.Equal(0, noPlay.TrialReminded);
        Assert.Empty(_notifications.Inserted);
        Assert.Equal(0, _users.CandidateSearches);
        // Not set aside either: if Plus goes live later, both are still owed their notices.
        Assert.Null(_users.TrialReminderStage["user-1"]);
        Assert.Null(_users.TrialReminderStage["user-2"]);
    }

    // ---- Settings ---------------------------------------------------------------------------

    [Fact]
    public async Task With_no_free_days_there_is_nothing_to_warn_anyone_about()
    {
        Account("user-1", daysAgo: 24);

        var pass = await Run(CreateHandler(trialDays: 0));

        Assert.Equal(0, pass.TrialReminded);
        Assert.Equal(0, _users.CandidateSearches);
    }

    [Fact]
    public async Task Either_notice_can_be_switched_off_on_its_own()
    {
        Account("week-out", daysAgo: 24);
        Account("day-out", daysAgo: 29.5, stage: 1);

        var firstOnly = await Run(CreateHandler(finalReminderDays: 0));

        Assert.Equal(1, firstOnly.TrialReminded);
        Assert.Equal("week-out", Assert.Single(_notifications.Inserted).UserId);

        var lastOnly = await Run(CreateHandler(reminderDays: 0));

        Assert.Equal(1, lastOnly.TrialReminded);
        Assert.Equal("day-out", _notifications.Inserted[1].UserId);
    }

    // ---- Size -------------------------------------------------------------------------------

    /// <summary>
    /// Everyone who was already there when Plus went live comes due in the same hour. They are
    /// worked through a batch at a time rather than in one long pass, and nobody is dropped.
    /// </summary>
    [Fact]
    public async Task A_backlog_is_worked_off_over_several_passes_and_everyone_is_told_exactly_once()
    {
        for (var i = 0; i < 450; i++)
        {
            Account($"user-{i:000}", daysAgo: 24 + (i / 1000.0));
        }

        var first = await Run(CreateHandler());
        var second = await Run(CreateHandler());
        var third = await Run(CreateHandler());
        var fourth = await Run(CreateHandler());

        Assert.Equal(200, first.TrialReminded);
        Assert.Equal(200, second.TrialReminded);
        Assert.Equal(50, third.TrialReminded);
        Assert.Equal(0, fourth.TrialReminded);
        Assert.Equal(450, _notifications.Inserted.Select(x => x.UserId).Distinct().Count());
        Assert.Equal(450, _notifications.Inserted.Count);
    }

    /// <summary>The date in the title is the one on the reader's own calendar.</summary>
    [Fact]
    public async Task The_notice_names_the_day_in_the_accounts_own_time_zone()
    {
        // Ends at 23:30 UTC six days from today, which is already the following day in Tokyo.
        var endsAt = Now.Date.AddDays(6).AddHours(23).AddMinutes(30);
        _users.Add("user-1", endsAt.AddDays(-30));
        _preferences.TimeZones["user-1"] = "Asia/Tokyo";

        await Run(CreateHandler());

        var notification = Assert.Single(_notifications.Inserted);

        Assert.Equal(PlusTrialReminder.FirstTitle(endsAt, "Asia/Tokyo"), notification.Title);
        Assert.NotEqual(PlusTrialReminder.FirstTitle(endsAt, null), notification.Title);
    }
}
