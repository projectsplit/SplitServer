using MediatR;
using Serilog;
using SplitServer.Commands;

namespace SplitServer.Services;

/// <summary>
/// Ticks the recurring expense schedule from inside the app, so the feature needs nothing wired up
/// outside it to work. The tick is far finer than any supported cycle, which keeps a generated
/// expense from showing up noticeably late without the interval itself carrying any meaning — the
/// due time on each template is the only thing that decides when it fires.
/// </summary>
public class RecurringExpensesWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Late enough that the first pass does not compete with startup, early enough that a restart
    /// still drains anything the downtime made due.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RecurringExpensesWorker(IServiceScopeFactory serviceScopeFactory)
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
            // stops it for the lifetime of the process, and every later occurrence with it.
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new ProcessDueRecurringExpensesCommand(), ct);

                if (result.IsSuccess && result.Value.Processed > 0)
                {
                    Log.Information(
                        "Recurring expenses pass processed {Processed}, created {Created}, failed {Failed}",
                        result.Value.Processed,
                        result.Value.Created,
                        result.Value.Failed);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Recurring expenses pass failed");
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
