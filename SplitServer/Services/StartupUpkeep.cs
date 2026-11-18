using Serilog;
using SplitServer.Repositories;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Services;

/// <summary>
/// The work done on every boot to bring the database into line with what the code now expects:
/// indexes, the grant of Plus for life, the moment Plus went live, the donation summaries.
///
/// Every step is idempotent, so running them all on every boot only ever converges, and there is
/// no migration to remember. None of them is fatal either. The app works without each — slower
/// without an index, and the rules that read the rest all have an answer for it being missing —
/// so a step that fails is logged and left for the next boot.
/// </summary>
public static class StartupUpkeep
{
    public static async Task Run(IServiceProvider services, CancellationToken ct)
    {
        await RunInOrder(
        [
            ("ensure Plus subscription indexes",
                () => services.GetRequiredService<IPlusSubscriptionsRepository>().EnsureIndexes(ct)),

            ("ensure user indexes",
                () => services.GetRequiredService<IUsersRepository>().EnsureIndexes(ct)),

            // Run whether or not Plus is switched on, so the grant is already on those accounts the
            // day it is. Only ever adds to accounts that have none.
            ("grant Plus for life to the accounts from before it cost anything",
                () => GrantPlusForLife(services, ct)),

            // Free days are counted from this moment, so it is settled here, by the deploy that
            // switched Plus on, rather than by whichever request happens to arrive first. Written
            // the first time this boots with Plus live and never moved again.
            ("record when Buqs Plus went live",
                () => services.GetRequiredService<PlusAccessService>().EnsureLaunched(DateTime.UtcNow, ct)),

            // Recomputed from the ledger rather than adjusted: gives a summary to gifts recorded
            // before summaries existed, and repairs one that a failed write left behind.
            ("refresh donation summaries",
                () => RefreshDonationSummaries(services, ct)),
        ]);
    }

    /// <summary>
    /// Runs each step in turn and never throws. Returns the names of the steps that completed.
    /// </summary>
    /// <remarks>
    /// One failure does not stop the rest, with a single exception: a timeout. That is what an
    /// unreachable database looks like, each step would wait the same half minute to find out the
    /// same thing, and the app would be that many half minutes late to start answering. So after
    /// the first timeout the remaining steps are skipped, to be picked up by the next boot.
    /// </remarks>
    public static async Task<IReadOnlyList<string>> RunInOrder(IEnumerable<(string What, Func<Task> Step)> steps)
    {
        var completed = new List<string>();
        var databaseIsUnreachable = false;

        foreach (var (what, step) in steps)
        {
            if (databaseIsUnreachable)
            {
                Log.Warning("Skipped at startup because the database could not be reached: {What}", what);

                continue;
            }

            try
            {
                await step();

                completed.Add(what);
            }
            catch (TimeoutException ex)
            {
                databaseIsUnreachable = true;

                Log.Warning(ex, "Startup could not {What}", what);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup could not {What}", what);
            }
        }

        return completed;
    }

    private static async Task GrantPlusForLife(IServiceProvider services, CancellationToken ct)
    {
        if (services.GetRequiredService<PlusCatalog>().LifetimeBefore is not { } lifetimeBefore)
        {
            return;
        }

        var granted = await services.GetRequiredService<IUsersRepository>()
            .GrantPlusForLifeToAccountsCreatedBefore(lifetimeBefore, DateTime.UtcNow, ct);

        if (granted.IsFailure)
        {
            throw new InvalidOperationException(granted.Error);
        }

        if (granted.Value > 0)
        {
            Log.Information(
                "Granted Plus for life to {Accounts} accounts created before {LifetimeBefore}",
                granted.Value,
                lifetimeBefore);
        }
    }

    private static async Task RefreshDonationSummaries(IServiceProvider services, CancellationToken ct)
    {
        var donors = await services.GetRequiredService<DonationSummaries>().RefreshAll(ct);

        if (donors > 0)
        {
            Log.Information("Refreshed the donation summary of {Donors} users", donors);
        }
    }
}
