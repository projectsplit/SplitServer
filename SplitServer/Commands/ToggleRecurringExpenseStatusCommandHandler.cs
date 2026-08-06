using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class ToggleRecurringExpenseStatusCommandHandler : IRequestHandler<ToggleRecurringExpenseStatusCommand, Result>
{
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;

    public ToggleRecurringExpenseStatusCommandHandler(IRecurringExpensesRepository recurringExpensesRepository)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
    }

    public async Task<Result> Handle(ToggleRecurringExpenseStatusCommand command, CancellationToken ct)
    {
        var templateMaybe = await _recurringExpensesRepository.GetById(command.RecurringExpenseId, ct);

        if (templateMaybe.HasNoValue)
        {
            return Result.Failure($"Recurring expense with id {command.RecurringExpenseId} was not found");
        }

        var template = templateMaybe.Value;

        if (template.UserId != command.UserId)
        {
            return Result.Failure("This recurring expense does not belong to user");
        }

        var now = DateTime.UtcNow;

        // Resuming skips whatever came due while paused rather than firing it all at once — a pause
        // the user meant as "not now" must not turn into a pile of expenses when they undo it.
        if (template.IsPaused && template.Schedule is null)
        {
            return Result.Failure("This recurring expense has no schedule. Edit it to set one, or delete it");
        }

        // The skip applies only to pauses the user chose. A template that paused itself after a
        // failed run still owes the slot that failed — its date was never a choice to skip — so it
        // stays due and the next pass backfills that one occurrence, dated when it was promised.
        var skipMissedOnResume = template.IsPaused
            && template.Schedule is not null
            && template.NextOccurrence <= now
            && template.LastError is null;

        var nextOccurrence = skipMissedOnResume
            ? RecurrenceCalculator.CatchUp(template.NextOccurrence, now, template.Schedule!, template.TimeZoneId).NextOccurrence
            : template.NextOccurrence;

        return await _recurringExpensesRepository.Update(
            template with
            {
                IsPaused = !template.IsPaused,
                NextOccurrence = nextOccurrence,
                LastError = template.IsPaused ? null : template.LastError,
                Updated = now
            },
            ct);
    }
}
