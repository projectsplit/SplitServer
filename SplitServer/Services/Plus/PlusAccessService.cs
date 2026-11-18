using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Donations;

namespace SplitServer.Services.Plus;

/// <summary>
/// Answers "what access does this account have" for the rest of the server. The rule itself is
/// <see cref="PlusAccessRule"/>; this supplies the half of its input that is the same for everyone
/// — whether Plus is live, since when, and on what terms — and remembers it, so that asking costs
/// nothing on a path every request takes.
/// </summary>
public class PlusAccessService
{
    /// <summary>
    /// How long to wait before looking for the launch moment again after failing to get it. Asked
    /// for on every request, so without this a database that is down would be asked on every one.
    /// </summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

    private readonly IPlaySubscriptions _play;
    private readonly PlusCatalog _catalog;
    private readonly IPlusStateRepository _plusStateRepository;

    private readonly SemaphoreSlim _loading = new(1, 1);

    /// <summary>The launch document once it has been read. It never changes, so it is read once.</summary>
    private volatile PlusLaunch? _launch;

    private long _retryNotBeforeTicks;

    public PlusAccessService(
        IPlaySubscriptions play,
        PlusCatalog catalog,
        IPlusStateRepository plusStateRepository)
    {
        _play = play;
        _catalog = catalog;
        _plusStateRepository = plusStateRepository;
    }

    /// <summary>Whether Plus is live on this instance: switched on, with Play there to verify a purchase.</summary>
    public bool IsOffered => _catalog.IsOffered(_play.IsConfigured);

    /// <summary>What is true for every account at <paramref name="now"/>.</summary>
    public async Task<PlusTerms> GetTerms(DateTime now, CancellationToken ct)
    {
        var isOffered = IsOffered;

        // Not asked for at all while Plus is not live: nothing reads it then, and an instance that
        // is not selling has no business deciding when selling began.
        var liveSince = isOffered ? await GetLiveSince(now, ct) : now;

        return new PlusTerms(isOffered, liveSince, _catalog.TrialDays, _catalog.LifetimeBefore);
    }

    /// <summary>The access <paramref name="user"/> has at <paramref name="now"/>.</summary>
    public async Task<PlusAccess> For(User user, DateTime now, CancellationToken ct) =>
        PlusAccessRule.Evaluate(user, await GetTerms(now, ct), now);

    /// <summary>
    /// Records the moment Plus went live, the first time this is called with Plus live, and does
    /// nothing on every call after that. Returns the moment that stands, or null while Plus is not
    /// live.
    /// </summary>
    /// <remarks>
    /// Called on boot so that the moment is fixed by the deploy that switched Plus on rather than by
    /// whichever request happened to arrive first. Throws if the database cannot be reached; the
    /// caller decides that is not fatal, and <see cref="GetTerms"/> tries again by itself.
    /// </remarks>
    public async Task<DateTime?> EnsureLaunched(DateTime now, CancellationToken ct)
    {
        if (!IsOffered)
        {
            return null;
        }

        await _loading.WaitAsync(ct);

        try
        {
            return await LoadOrCreate(now, ct);
        }
        finally
        {
            _loading.Release();
        }
    }

    private async Task<DateTime> GetLiveSince(DateTime now, CancellationToken ct)
    {
        if (_launch is { } known)
        {
            return known.LiveSince;
        }

        // Only reached when boot could not fix the moment. Until it is fixed, Plus is treated as
        // having gone live this instant, which errs entirely on the side of the account: nobody's
        // free days run down against a start that was never written. One request at a time goes
        // looking, no more often than the retry interval; the rest are answered without waiting.
        if (now.Ticks < Interlocked.Read(ref _retryNotBeforeTicks) || !await _loading.WaitAsync(0, ct))
        {
            return now;
        }

        try
        {
            if (_launch is { } loaded)
            {
                return loaded.LiveSince;
            }

            Interlocked.Exchange(ref _retryNotBeforeTicks, (now + RetryInterval).Ticks);

            return await LoadOrCreate(now, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Could not read when Buqs Plus went live; treating it as now");

            return now;
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>Must be called holding <see cref="_loading"/>.</summary>
    private async Task<DateTime> LoadOrCreate(DateTime now, CancellationToken ct)
    {
        if (_launch is { } known)
        {
            return known.LiveSince;
        }

        var launchMaybe = await _plusStateRepository.GetLaunch(ct);

        if (launchMaybe.HasNoValue)
        {
            var insertResult = await _plusStateRepository.InsertLaunchIfAbsent(
                new PlusLaunch
                {
                    Id = PlusLaunch.DocumentId,
                    Created = now,
                    Updated = now,
                    LiveSince = now,
                },
                ct);

            if (insertResult.IsFailure)
            {
                throw new InvalidOperationException($"Could not record when Buqs Plus went live: {insertResult.Error}");
            }

            // Read back rather than assumed: if another instance got there first, its moment is the
            // one that stands, and this instance has to count from the same one.
            launchMaybe = await _plusStateRepository.GetLaunch(ct);

            if (launchMaybe.HasNoValue)
            {
                throw new InvalidOperationException("The Plus launch moment was written but could not be read back");
            }

            Log.Information("Buqs Plus is live from {LiveSince}", launchMaybe.Value.LiveSince);
        }

        _launch = launchMaybe.Value;

        return launchMaybe.Value.LiveSince;
    }
}
