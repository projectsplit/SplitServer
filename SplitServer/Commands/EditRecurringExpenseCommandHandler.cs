using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

/// <summary>
/// Edits the template only. Expenses it already produced are ordinary expenses and stay exactly as
/// they are — the user edits or deletes those from their own list, as they would any other.
/// </summary>
public class EditRecurringExpenseCommandHandler : IRequestHandler<EditRecurringExpenseCommand, Result>
{
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly RecurringExpenseValidator _validator;

    public EditRecurringExpenseCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        IUserPreferencesRepository userPreferencesRepository,
        RecurringExpenseValidator validator)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _validator = validator;
    }

    public async Task<Result> Handle(EditRecurringExpenseCommand command, CancellationToken ct)
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

        // Re-read rather than kept from creation: the schedule the user is looking at is labelled
        // with their current zone, so that is the zone the edited schedule must be read in.
        var userPreferencesMaybe = await _userPreferencesRepository.GetById(command.UserId, ct);

        var timeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? template.TimeZoneId
            : template.TimeZoneId;

        // A changed schedule is re-resolved from scratch: moving rent from the 1st to the 15th has
        // to land on the 15th, not on whatever the old schedule had already queued up. A changed
        // zone re-resolves too — the same wall clock time is a different instant there. What is
        // deliberately kept is the slot of a run that failed, so fixing the template backfills the
        // occurrence that failed instead of dropping it.
        var scheduleUnchanged = command.Schedule == template.Schedule && timeZoneId == template.TimeZoneId;

        var nextOccurrence = scheduleUnchanged
            ? template.NextOccurrence
            : RecurrenceCalculator.GetFirstOccurrence(now, command.Schedule, timeZoneId);

        var updated = ApplyPayload(template, command) with
        {
            Amount = command.Amount,
            Currency = command.Currency,
            Description = command.Description,
            Location = command.Location,
            Labels = command.Labels,
            Schedule = command.Schedule,
            TimeZoneId = timeZoneId,
            NextOccurrence = nextOccurrence,
            Updated = now,
            // An edit is the user's answer to whatever went wrong, so a template that paused itself
            // after a failed run starts running again. A pause the user chose stays a pause — they
            // said stop, and editing the amount is not saying start.
            IsPaused = template.IsPaused && template.LastError is null,
            LastError = null
        };

        // Checked before saving rather than left for the next run to discover. An unbalanced split
        // saved now would sit quietly until the cycle came round, then pause itself with an error
        // the user has long since stopped associating with this edit.
        var validationResult = await _validator.Validate(updated, ct);

        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        return await _recurringExpensesRepository.Update(updated, ct);
    }

    private static RecurringExpense ApplyPayload(RecurringExpense template, EditRecurringExpenseCommand command)
    {
        return template switch
        {
            GroupRecurringExpense t => t with
            {
                Payments = command.Payments ?? t.Payments,
                Shares = command.Shares ?? t.Shares
            },
            NonGroupRecurringExpense t => t with
            {
                Payments = command.NonGroupPayments ?? t.Payments,
                Shares = command.NonGroupShares ?? t.Shares
            },
            _ => template
        };
    }
}
