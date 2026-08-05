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
    private readonly RecurringExpenseValidator _validator;

    public EditRecurringExpenseCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        RecurringExpenseValidator validator)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
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

        // A changed schedule is re-resolved from scratch: moving rent from the 1st to the 15th has
        // to land on the 15th, not on whatever the old schedule had already queued up.
        var nextOccurrence = command.Schedule == template.Schedule
            ? template.NextOccurrence
            : RecurrenceCalculator.GetFirstOccurrence(now, command.Schedule, template.TimeZoneId);

        var updated = ApplyPayload(template, command) with
        {
            Amount = command.Amount,
            Currency = command.Currency,
            Description = command.Description,
            Location = command.Location,
            Labels = command.Labels,
            Schedule = command.Schedule,
            NextOccurrence = nextOccurrence,
            Updated = now,
            // An edit is the user's answer to whatever went wrong, so let it run again.
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
