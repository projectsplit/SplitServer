using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The moment Plus went live, and whether it is live at all. Every account's free days are counted
/// from that moment, so the properties that matter are that it is written once, that it is never
/// moved, and that not having it yet can only ever err in an account's favour.
/// </summary>
public class PlusAccessServiceTests
{
    private static readonly DateTime Boot = new(2027, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly FakePlay _play = new();
    private readonly FakePlusStateRepository _state = new();

    private PlusAccessService CreateService(bool enabled = true, PlusSettings? settings = null) =>
        new(_play, new PlusCatalog(Options.Create(settings ?? new PlusSettings { Enabled = enabled })), _state);

    private static User Account(DateTime created) =>
        new()
        {
            Id = "user-1",
            Created = created,
            Updated = created,
            Email = null,
            EmailVerified = false,
            HashedPassword = null,
            Username = "someone",
            GoogleId = null,
        };

    // ---- Whether Plus is live ---------------------------------------------------------------

    [Fact]
    public void Plus_is_live_only_when_it_is_switched_on_and_Play_can_be_asked()
    {
        Assert.True(CreateService(enabled: true).IsOffered);
        Assert.False(CreateService(enabled: false).IsOffered);

        _play.IsConfigured = false;

        Assert.False(CreateService(enabled: true).IsOffered);
    }

    /// <summary>
    /// An instance that is not selling has no business deciding when selling began. Nothing is read
    /// and nothing is written, so switching Plus on later starts from a clean database.
    /// </summary>
    [Fact]
    public async Task While_Plus_is_not_live_the_launch_is_neither_recorded_nor_looked_for()
    {
        var service = CreateService(enabled: false);

        Assert.Null(await service.EnsureLaunched(Boot, CancellationToken.None));

        var terms = await service.GetTerms(Boot.AddDays(3), CancellationToken.None);

        Assert.False(terms.IsOffered);
        Assert.Null(_state.Launch);
        Assert.Equal(0, _state.Reads);
        Assert.Equal(0, _state.Writes);
    }

    [Fact]
    public async Task While_Plus_is_not_live_every_account_is_open()
    {
        var access = await CreateService(enabled: false).For(Account(Boot.AddYears(-1)), Boot, CancellationToken.None);

        Assert.Equal(PlusAccessKind.Open, access.Kind);
    }

    // ---- The launch moment ------------------------------------------------------------------

    [Fact]
    public async Task The_first_boot_with_Plus_live_records_the_moment()
    {
        var liveSince = await CreateService().EnsureLaunched(Boot, CancellationToken.None);

        Assert.Equal(Boot, liveSince);
        Assert.NotNull(_state.Launch);
        Assert.Equal(PlusLaunch.DocumentId, _state.Launch.Id);
        Assert.Equal(Boot, _state.Launch.LiveSince);
    }

    /// <summary>
    /// Every restart and every other instance has to count from the same moment, or the free days
    /// of every account older than Plus would start again with each deploy.
    /// </summary>
    [Fact]
    public async Task Later_boots_keep_the_moment_that_was_recorded_first()
    {
        await CreateService().EnsureLaunched(Boot, CancellationToken.None);

        var afterRestart = await CreateService().EnsureLaunched(Boot.AddDays(40), CancellationToken.None);
        var terms = await CreateService().GetTerms(Boot.AddDays(41), CancellationToken.None);

        Assert.Equal(Boot, afterRestart);
        Assert.Equal(Boot, terms.LiveSince);
        Assert.Equal(Boot, _state.Launch?.LiveSince);
        Assert.Equal(1, _state.Writes);
    }

    [Fact]
    public async Task Switching_Plus_off_and_on_again_does_not_move_the_moment()
    {
        await CreateService(enabled: true).EnsureLaunched(Boot, CancellationToken.None);
        await CreateService(enabled: false).EnsureLaunched(Boot.AddDays(10), CancellationToken.None);

        var backOn = await CreateService(enabled: true).EnsureLaunched(Boot.AddDays(20), CancellationToken.None);

        Assert.Equal(Boot, backOn);
    }

    /// <summary>Asked on every request, so after the first it must cost nothing.</summary>
    [Fact]
    public async Task Once_known_the_moment_is_not_read_again()
    {
        var service = CreateService();
        await service.EnsureLaunched(Boot, CancellationToken.None);

        var readsAfterBoot = _state.Reads;

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(Boot, (await service.GetTerms(Boot.AddHours(i), CancellationToken.None)).LiveSince);
        }

        Assert.Equal(readsAfterBoot, _state.Reads);
    }

    /// <summary>
    /// Boot could not reach the database, so the moment was never fixed. The first request to ask
    /// fixes it, and the answer from then on is the same.
    /// </summary>
    [Fact]
    public async Task A_moment_that_boot_could_not_record_is_recorded_by_the_first_request()
    {
        var service = CreateService();

        var first = await service.GetTerms(Boot, CancellationToken.None);
        var later = await service.GetTerms(Boot.AddDays(12), CancellationToken.None);

        Assert.Equal(Boot, first.LiveSince);
        Assert.Equal(Boot, later.LiveSince);
        Assert.Equal(Boot, _state.Launch?.LiveSince);
    }

    /// <summary>
    /// With no moment to count from, Plus is treated as having gone live this instant. That can only
    /// ever give an account more free days, never fewer, and it must not fail the request.
    /// </summary>
    [Fact]
    public async Task While_the_moment_cannot_be_read_Plus_counts_as_live_from_now()
    {
        _state.IsDown = true;
        var service = CreateService();

        await Assert.ThrowsAsync<TimeoutException>(() => service.EnsureLaunched(Boot, CancellationToken.None));

        // Made after the cut-off for Plus for life, and a month before anything here happens.
        var account = Account(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        var asked = Boot.AddDays(5);
        var terms = await service.GetTerms(asked, CancellationToken.None);
        var access = await service.For(account, asked, CancellationToken.None);

        Assert.True(terms.IsOffered);
        Assert.Equal(asked, terms.LiveSince);
        Assert.Equal(PlusAccessKind.Trial, access.Kind);
        Assert.Equal(asked.AddDays(30), access.TrialEndsAt);
    }

    [Fact]
    public async Task A_database_that_is_down_is_not_asked_again_on_every_request()
    {
        var service = CreateService();
        _state.IsDown = true;

        await service.GetTerms(Boot, CancellationToken.None);

        _state.IsDown = false;

        // Seconds later, and still inside the pause: answered without touching the database.
        await service.GetTerms(Boot.AddSeconds(5), CancellationToken.None);
        await service.GetTerms(Boot.AddSeconds(30), CancellationToken.None);

        Assert.Equal(0, _state.Reads);
        Assert.Null(_state.Launch);

        // After the pause it looks again, finds nothing there, and fixes the moment.
        var recovered = await service.GetTerms(Boot.AddMinutes(2), CancellationToken.None);

        Assert.Equal(Boot.AddMinutes(2), recovered.LiveSince);
        Assert.Equal(Boot.AddMinutes(2), _state.Launch?.LiveSince);
    }

    // ---- The terms --------------------------------------------------------------------------

    [Fact]
    public async Task The_terms_are_the_ones_that_were_configured()
    {
        var cutOff = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var service = CreateService(
            settings: new PlusSettings { Enabled = true, TrialDays = 14, LifetimeBefore = cutOff });

        await service.EnsureLaunched(Boot, CancellationToken.None);

        var terms = await service.GetTerms(Boot.AddDays(1), CancellationToken.None);

        Assert.Equal(new PlusTerms(IsOffered: true, LiveSince: Boot, TrialDays: 14, LifetimeBefore: cutOff), terms);
    }

    [Fact]
    public async Task An_account_is_judged_by_the_rule_on_those_terms()
    {
        var service = CreateService();
        await service.EnsureLaunched(Boot, CancellationToken.None);

        var now = Boot.AddDays(100);

        var founder = await service.For(Account(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)), now, CancellationToken.None);
        var newcomer = await service.For(Account(now.AddDays(-3)), now, CancellationToken.None);
        var lapsed = await service.For(Account(now.AddDays(-60)), now, CancellationToken.None);

        Assert.Equal(PlusAccessKind.Lifetime, founder.Kind);
        Assert.Equal(PlusAccessKind.Trial, newcomer.Kind);
        Assert.Equal(PlusAccessKind.None, lapsed.Kind);
    }
}
