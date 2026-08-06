using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

/// <summary>
/// Materializes every template that has come due. Safe to run at any cadence and from more than one
/// trigger: a template is advanced past its due time as soon as it is handled, so a second pass
/// crossing the first finds nothing left to do.
/// </summary>
public class ProcessDueRecurringExpensesCommandHandler
    : IRequestHandler<ProcessDueRecurringExpensesCommand, Result<ProcessDueRecurringExpensesResponse>>
{
    private const string LockKey = "process-due-recurring-expenses";

    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly LockService _lockService;
    private readonly IMediator _mediator;

    public ProcessDueRecurringExpensesCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        LockService lockService,
        IMediator mediator)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _lockService = lockService;
        _mediator = mediator;
    }

    public async Task<Result<ProcessDueRecurringExpensesResponse>> Handle(
        ProcessDueRecurringExpensesCommand command,
        CancellationToken ct)
    {
        // The worker and the manual endpoint can fire at the same time, and both would read the
        // same due templates before either advanced them — one occurrence written twice. Only one
        // pass runs at a time; the other is told to come back rather than duplicating it.
        IDisposable pass;

        try
        {
            pass = _lockService.AcquireLock(LockKey);
        }
        catch (ResourceLockedException)
        {
            return Result.Failure<ProcessDueRecurringExpensesResponse>(
                "A recurring expenses pass is already running");
        }

        using (pass)
        {
            return await Process(command, ct);
        }
    }

    private async Task<Result<ProcessDueRecurringExpensesResponse>> Process(
        ProcessDueRecurringExpensesCommand command,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var dueTemplates = await _recurringExpensesRepository.GetDue(now, command.BatchSize, ct);

        var created = 0;
        var failed = 0;

        foreach (var template in dueTemplates)
        {
            ct.ThrowIfCancellationRequested();

            var occurrenceResult = await CreateOccurrence(template, ct);

            if (occurrenceResult.IsSuccess)
            {
                created++;
            }
            else
            {
                failed++;
            }
        }

        return new ProcessDueRecurringExpensesResponse
        {
            Processed = dueTemplates.Count,
            Created = created,
            Failed = failed
        };
    }

    private async Task<Result> CreateOccurrence(RecurringExpense template, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // A template with no readable schedule has no next date to compute, and letting that throw
        // would abort the whole pass — one unusable row would stop every other user's expenses.
        // Paused instead, which both records why and takes it out of the due query for good.
        if (template.Schedule is null)
        {
            Log.Warning("Recurring expense {RecurringExpenseId} has no schedule and was paused", template.Id);

            var pauseUnusableResult = await _recurringExpensesRepository.UpdateIfUnchanged(
                template with
                {
                    IsPaused = true,
                    LastRunAt = now,
                    LastError = "This recurring expense has no schedule. Edit it to set one, or delete it",
                    Updated = now
                },
                template.Updated,
                ct);

            // A concurrent write is fine here: whatever the user just did supersedes this pause,
            // and if the template is still unrunnable the next pass pauses the fresh copy.
            return pauseUnusableResult.IsFailure
                ? Result.Failure(pauseUnusableResult.Error)
                : Result.Failure("Missing schedule");
        }

        // The occurrence is dated when it was due, not when the worker got to it, so a late run does
        // not misplace the expense in the user's timeline.
        var dueAt = template.NextOccurrence;

        var (nextOccurrence, skipped) = RecurrenceCalculator.CatchUp(dueAt, now, template.Schedule, template.TimeZoneId);

        if (skipped > 0)
        {
            Log.Information(
                "Recurring expense {RecurringExpenseId} skipped {SkippedOccurrences} missed occurrences",
                template.Id,
                skipped);
        }

        Result<string> occurrenceResult;

        try
        {
            occurrenceResult = await RecurringExpenseOccurrence.Create(_mediator, template, dueAt, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Recurring expense {RecurringExpenseId} threw while creating an occurrence", template.Id);
            occurrenceResult = Result.Failure<string>(ex.Message);
        }

        if (occurrenceResult.IsFailure)
        {
            Log.Warning(
                "Recurring expense {RecurringExpenseId} could not be created and was paused: {Error}",
                template.Id,
                occurrenceResult.Error);

            // Pausing rather than retrying forever. The causes are permanent until the user acts —
            // the group was deleted, a member was removed, a connection was revoked — and the manage
            // list shows the reason next to a resume they can use once it is sorted out.
            //
            // NextOccurrence deliberately stays at the slot that failed. The expense was promised
            // for that date; once the user fixes the cause and the template runs again, it is
            // created then rather than silently never existing.
            var pauseResult = await _recurringExpensesRepository.UpdateIfUnchanged(
                template with
                {
                    IsPaused = true,
                    LastRunAt = now,
                    LastError = occurrenceResult.Error,
                    Updated = now
                },
                template.Updated,
                ct);

            // On a concurrent write the pause is dropped, not forced: the user's edit may be the
            // very fix, and the next pass retries against their version and pauses it only if it
            // still fails.
            return pauseResult.IsFailure ? Result.Failure(pauseResult.Error) : Result.Failure(occurrenceResult.Error);
        }

        var advanceResult = await _recurringExpensesRepository.UpdateIfUnchanged(
            template with
            {
                NextOccurrence = nextOccurrence,
                LastExpenseId = occurrenceResult.Value,
                LastRunAt = now,
                LastError = null,
                Updated = now
            },
            template.Updated,
            ct);

        if (advanceResult.IsFailure)
        {
            return Result.Failure(advanceResult.Error);
        }

        return advanceResult.Value
            ? Result.Success()
            : await RecordOccurrenceOnFreshTemplate(template.Id, occurrenceResult.Value, dueAt, now, ct);
    }

    /// <summary>
    /// The expense for this slot exists; what failed was recording that on the template, because a
    /// user write landed while the occurrence was being created. Their write wins on every field —
    /// this only makes the template agree the slot fired, so the next pass does not fire it twice.
    /// </summary>
    private async Task<Result> RecordOccurrenceOnFreshTemplate(
        string templateId,
        string expenseId,
        DateTime dueAt,
        DateTime now,
        CancellationToken ct)
    {
        const int maxAttempts = 3;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var freshMaybe = await _recurringExpensesRepository.GetById(templateId, ct);

            if (freshMaybe.HasNoValue)
            {
                // Deleted mid-run. The expense stands — it was due when the pass started — and
                // there is no template left to advance.
                return Result.Success();
            }

            var fresh = freshMaybe.Value;

            // An edit that re-resolved the schedule already points past the slot; only a next
            // occurrence still at or before the one just materialized needs pushing forward.
            var nextOccurrence = fresh.Schedule is not null && fresh.NextOccurrence <= dueAt
                ? RecurrenceCalculator.CatchUp(dueAt, now, fresh.Schedule, fresh.TimeZoneId).NextOccurrence
                : fresh.NextOccurrence;

            var recordResult = await _recurringExpensesRepository.UpdateIfUnchanged(
                fresh with
                {
                    NextOccurrence = nextOccurrence,
                    LastExpenseId = expenseId,
                    LastRunAt = now,
                    Updated = now
                },
                fresh.Updated,
                ct);

            if (recordResult.IsFailure)
            {
                return Result.Failure(recordResult.Error);
            }

            if (recordResult.Value)
            {
                return Result.Success();
            }
        }

        return Result.Failure($"Recurring expense {templateId} kept changing while its occurrence was being recorded");
    }
}
