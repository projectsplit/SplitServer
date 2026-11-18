using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.Donations;
using SplitServer.Services.Plus;

namespace SplitServer.Commands;

/// <summary>
/// Keeps Buqs Plus true over time without waiting to be told.
///
/// Play can push a notification whenever a subscription renews, lapses or is refunded, but that
/// depends on a Pub/Sub hookup outside this codebase, and a notification can be lost even when it
/// exists. So nothing here assumes one will come. Every live subscription is re-read from Google
/// when its paid period runs out, and at least once a day regardless — which catches a renewal and
/// an expiry on time, and a refund or a revocation within a day, with no notification at all.
///
/// The second job is the renewal notice for annual plans. It runs after the re-read, on purpose, so
/// that it speaks from what Google says now rather than from a row that might be a day old.
///
/// The third is the notice that an account's free days are about to end, a week out and a day out,
/// while Plus is live. It comes last for the same reason: someone who subscribed an hour ago should
/// be seen as a subscriber by the time anything asks whether they still need telling.
///
/// Safe at any cadence and to run twice: a re-read sets every field to what Google said, and each
/// reminder is claimed with a single conditional write before it is sent.
/// </summary>
public class ProcessPlusSubscriptionsCommandHandler
    : IRequestHandler<ProcessPlusSubscriptionsCommand, Result<ProcessPlusSubscriptionsResponse>>
{
    private const string LockKey = "process-plus-subscriptions";

    /// <summary>
    /// The most accounts looked at for each trial notice on one pass. A backlog — everyone who was
    /// already there when Plus went live comes due in the same hour — is worked off over a few
    /// passes rather than in one long one, and a notice a few hours late is still days early.
    /// </summary>
    private const int TrialReminderBatchSize = 200;

    private readonly IPlaySubscriptions _play;
    private readonly PlusCatalog _catalog;
    private readonly PlusRecorder _recorder;
    private readonly PlusAccessService _accessService;
    private readonly IPlusSubscriptionsRepository _plusSubscriptionsRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly NotificationService _notificationService;
    private readonly LockService _lockService;

    public ProcessPlusSubscriptionsCommandHandler(
        IPlaySubscriptions play,
        PlusCatalog catalog,
        PlusRecorder recorder,
        PlusAccessService accessService,
        IPlusSubscriptionsRepository plusSubscriptionsRepository,
        IUsersRepository usersRepository,
        IUserPreferencesRepository userPreferencesRepository,
        NotificationService notificationService,
        LockService lockService)
    {
        _play = play;
        _catalog = catalog;
        _recorder = recorder;
        _accessService = accessService;
        _plusSubscriptionsRepository = plusSubscriptionsRepository;
        _usersRepository = usersRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _notificationService = notificationService;
        _lockService = lockService;
    }

    public async Task<Result<ProcessPlusSubscriptionsResponse>> Handle(
        ProcessPlusSubscriptionsCommand command,
        CancellationToken ct)
    {
        // Without Play there is nobody to ask, so there is nothing for a pass to do. Rows and marks
        // are left exactly as they are; whether a mark still counts is decided against the clock
        // wherever it is read, with or without this worker.
        if (!_play.IsConfigured)
        {
            return new ProcessPlusSubscriptionsResponse { Refreshed = 0, Failed = 0, Reminded = 0, TrialReminded = 0 };
        }

        IDisposable pass;

        try
        {
            pass = _lockService.AcquireLock(LockKey);
        }
        catch (ResourceLockedException)
        {
            return Result.Failure<ProcessPlusSubscriptionsResponse>("A Plus subscriptions pass is already running");
        }

        using (pass)
        {
            var (refreshed, failed) = await RefreshDue(ct);
            var reminded = await SendRenewalReminders(ct);
            var trialReminded = await SendTrialReminders(ct);

            return new ProcessPlusSubscriptionsResponse
            {
                Refreshed = refreshed,
                Failed = failed,
                Reminded = reminded,
                TrialReminded = trialReminded,
            };
        }
    }

    private async Task<(int Refreshed, int Failed)> RefreshDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var live = await _plusSubscriptionsRepository.GetNotInStates(PlusEntitlement.EndedStates, ct);

        var refreshed = 0;
        var failed = 0;

        foreach (var subscription in live.Where(x => PlusEntitlement.IsRefreshDue(x, now)))
        {
            ct.ThrowIfCancellationRequested();

            // One subscription Google will not answer for must not cost everybody after it their
            // renewal, so each is tried on its own and a failure only counts.
            if (await TryReRead(subscription, ct))
            {
                refreshed++;
            }
            else
            {
                failed++;
            }
        }

        return (refreshed, failed);
    }

    private async Task<int> SendRenewalReminders(CancellationToken ct)
    {
        var reminderDays = _catalog.RenewalReminderDays;

        if (reminderDays <= 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        // Read again rather than reusing the list from the refresh above, which that pass has just
        // made stale for every row it touched.
        var live = await _plusSubscriptionsRepository.GetNotInStates(PlusEntitlement.EndedStates, ct);

        var reminded = 0;

        foreach (var candidate in live.Where(x => PlusEntitlement.IsReminderDue(x, now, reminderDays)))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (await SendRenewalReminder(candidate, reminderDays, ct))
                {
                    reminded++;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log.Error(ex, "Failed to send a Plus renewal reminder to user {UserId}", candidate.UserId);
            }
        }

        return reminded;
    }

    private async Task<bool> SendRenewalReminder(PlusSubscription candidate, int reminderDays, CancellationToken ct)
    {
        // Asked afresh rather than read off the row. The row can be most of a day old, and someone
        // who cancelled this morning must not be told this afternoon that their plan is about to
        // renew. If Google cannot be asked, the row on file is the best there is, and a reminder
        // that is right as far as anyone knows beats a renewal nobody was warned about.
        await TryReRead(candidate, ct);

        var currentMaybe = await _plusSubscriptionsRepository.GetById(candidate.Id, ct);

        if (currentMaybe.HasNoValue)
        {
            return false;
        }

        var subscription = currentMaybe.Value;

        if (!PlusEntitlement.IsReminderDue(subscription, DateTime.UtcNow, reminderDays) ||
            subscription.ExpiresAt is not { } renewsAt)
        {
            return false;
        }

        // Claimed before it is sent. See the repository for why that order and not the other.
        if (!await _plusSubscriptionsRepository.TryClaimRenewalReminder(subscription.Id, renewsAt, ct))
        {
            return false;
        }

        await _notificationService.Notify(
            [subscription.UserId],
            PlusRenewalReminder.Title,
            PlusRenewalReminder.Body(
                renewsAt,
                await TimeZoneOf(subscription.UserId, ct),
                subscription.RenewalPrice,
                subscription.RenewalCurrency),
            PlusRenewalReminder.Url,
            ct);

        return true;
    }

    /// <summary>
    /// Tells accounts whose free days are nearly over, while Plus is live. Returns how many notices
    /// went out.
    /// </summary>
    /// <remarks>
    /// Each notice has its own search, and each search asks only for accounts old enough to be
    /// inside that notice's window and not yet past its stage. So a pass reads the accounts that
    /// are due and no others: nobody too new to matter, nobody who has had the notice already, and
    /// nobody waiting between the first and the last.
    ///
    /// The last notice is dealt with first. An account that turns up inside both windows at once
    /// gets that one alone, and is at the final stage by the time the search for the first runs.
    /// </remarks>
    private async Task<int> SendTrialReminders(CancellationToken ct)
    {
        if (!_accessService.IsOffered)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var terms = await _accessService.GetTerms(now, ct);

        if (terms.TrialDays <= 0)
        {
            return 0;
        }

        var reminderDays = _catalog.TrialReminderDays;
        var finalReminderDays = _catalog.TrialFinalReminderDays;

        (int Stage, int Days)[] notices =
        [
            (PlusTrialReminder.DoneStage, finalReminderDays),
            (PlusTrialReminder.FirstStage, reminderDays),
        ];

        var sent = 0;

        foreach (var (stage, days) in notices)
        {
            if (days <= 0)
            {
                continue;
            }

            // Free days that began on or before this moment have at most `days` of them left.
            var beganOnOrBefore = now.AddDays(-Math.Max(0, terms.TrialDays - days));

            // Nobody's free days can have begun before Plus went live. Until that moment is far
            // enough back, no account can be inside this window, however old it is.
            if (terms.LiveSince > beganOnOrBefore)
            {
                continue;
            }

            var candidates = await _usersRepository.GetTrialReminderCandidates(
                beganOnOrBefore,
                stage,
                terms.LifetimeBefore,
                TrialReminderBatchSize,
                ct);

            foreach (var user in candidates)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (await SendTrialReminder(user, terms, reminderDays, finalReminderDays, ct))
                    {
                        sent++;
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Log.Error(ex, "Failed to send a Plus trial reminder to user {UserId}", user.Id);
                }
            }
        }

        return sent;
    }

    private async Task<bool> SendTrialReminder(
        User user,
        PlusTerms terms,
        int reminderDays,
        int finalReminderDays,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var access = PlusAccessRule.Evaluate(user, terms, now);

        var action = PlusTrialReminder.Decide(
            access,
            user.PlusTrialReminderStage ?? 0,
            now,
            reminderDays,
            finalReminderDays);

        if (action == PlusTrialReminderAction.None)
        {
            return false;
        }

        if (action == PlusTrialReminderAction.MarkDone)
        {
            // Subscribed, has Plus for life, or the free days are already over. Nothing is sent;
            // the account is only taken out of every later search.
            await _usersRepository.TryAdvanceTrialReminderStage(user.Id, PlusTrialReminder.DoneStage, ct);

            return false;
        }

        // Either notice is only ever decided on for an account inside its free days, which always
        // has an end to name. Checked before anything is claimed all the same, so that a notice
        // with nothing to say can never use up the account's one chance of being told.
        if (access.TrialEndsAt is not { } trialEndsAt)
        {
            return false;
        }

        var isFinal = action == PlusTrialReminderAction.SendFinal;

        // Claimed before it is sent. See the repository for why that order and not the other.
        if (!await _usersRepository.TryAdvanceTrialReminderStage(
                user.Id,
                isFinal ? PlusTrialReminder.DoneStage : PlusTrialReminder.FirstStage,
                ct))
        {
            return false;
        }

        var timeZone = await TimeZoneOf(user.Id, ct);

        await _notificationService.Notify(
            [user.Id],
            isFinal
                ? PlusTrialReminder.FinalTitle(trialEndsAt, now, timeZone)
                : PlusTrialReminder.FirstTitle(trialEndsAt, timeZone),
            isFinal
                ? PlusTrialReminder.FinalBody
                : PlusTrialReminder.FirstBody(trialEndsAt, now),
            PlusTrialReminder.Url,
            ct);

        return true;
    }

    /// <summary>
    /// The time zone a person's dates should be given in: the one they chose, or the same default
    /// the rest of the server assumes for someone who has not chosen.
    /// </summary>
    private async Task<string> TimeZoneOf(string userId, CancellationToken ct)
    {
        var preferencesMaybe = await _userPreferencesRepository.GetById(userId, ct);

        return preferencesMaybe.HasValue
            ? preferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;
    }

    /// <summary>
    /// Takes one subscription back to Google and says whether Google answered. Never throws for
    /// anything but the pass itself being cancelled.
    /// </summary>
    private async Task<bool> TryReRead(PlusSubscription subscription, CancellationToken ct)
    {
        try
        {
            var result = await _recorder.Record(subscription.Id, expectedUserId: null, ct);

            if (result.IsSuccess)
            {
                return true;
            }

            Log.Warning(
                "Could not re-read a Plus subscription of user {UserId} from Google Play: {Error}",
                subscription.UserId,
                result.Error);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Error(ex, "Re-reading a Plus subscription of user {UserId} failed", subscription.UserId);
        }

        // Google gave no answer, so the row stays as it was. The mark on the user is still brought
        // into line with the clock: a period that has visibly run out stops counting whether or not
        // anyone could confirm why, and comes back the moment a later re-read says it renewed.
        try
        {
            await _recorder.RefreshUser(subscription.UserId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Error(ex, "Could not refresh the Plus mark of user {UserId}", subscription.UserId);
        }

        return false;
    }
}
