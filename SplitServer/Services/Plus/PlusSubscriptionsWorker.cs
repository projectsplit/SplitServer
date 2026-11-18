using MediatR;
using Serilog;
using SplitServer.Commands;

namespace SplitServer.Services.Plus;

/// <summary>
/// Ticks the upkeep of Buqs Plus from inside the app: re-reading subscriptions whose period has run
/// out or that have not been looked at for a day, telling annual subscribers that a renewal is
/// close, and telling accounts that their free days are about to end. Nothing outside the process
/// has to be wired up for any of it to happen.
///
/// Play's own notifications do the first job faster when they are connected. This is what makes it
/// not matter when they are not, or when one goes missing.
/// </summary>
public class PlusSubscriptionsWorker : BackgroundService
{
    /// <summary>
    /// An hour is the longest a renewal can go unnoticed, and entitlement allows for a few of these
    /// before it treats a period that has run out as really over. Each pass is one filtered read
    /// plus a call to Google per subscription that is actually due, which on most passes is none.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// Late enough that the first pass does not compete with startup, early enough that a restart
    /// promptly catches up on whatever renewed or lapsed while the process was down.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PlusSubscriptionsWorker(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(StartupDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            // Never let one bad pass end the worker: an unhandled exception out of ExecuteAsync
            // stops it for the lifetime of the process, and every later renewal with it.
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new ProcessPlusSubscriptionsCommand(), ct);

                if (result.IsSuccess &&
                    (result.Value.Refreshed > 0 ||
                     result.Value.Failed > 0 ||
                     result.Value.Reminded > 0 ||
                     result.Value.TrialReminded > 0))
                {
                    Log.Information(
                        "Plus pass re-read {Refreshed} subscriptions, failed {Failed}, sent {Reminded} renewal and {TrialReminded} trial reminders",
                        result.Value.Refreshed,
                        result.Value.Failed,
                        result.Value.Reminded,
                        result.Value.TrialReminded);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Plus subscriptions pass failed");
            }
        }
        while (await SafeWaitForNextTick(timer, ct));
    }

    private static async Task<bool> SafeWaitForNextTick(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
