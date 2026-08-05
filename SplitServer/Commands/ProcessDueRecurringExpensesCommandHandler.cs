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
            var pauseResult = await _recurringExpensesRepository.Update(
                template with
                {
                    IsPaused = true,
                    NextOccurrence = nextOccurrence,
                    LastRunAt = now,
                    LastError = occurrenceResult.Error,
                    Updated = now
                },
                ct);

            return pauseResult.IsFailure ? pauseResult : Result.Failure(occurrenceResult.Error);
        }

        return await _recurringExpensesRepository.Update(
            template with
            {
                NextOccurrence = nextOccurrence,
                LastExpenseId = occurrenceResult.Value,
                LastRunAt = now,
                LastError = null,
                Updated = now
            },
            ct);
    }
}
