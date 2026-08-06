using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Commands;
using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// Turns a recurring template into a real expense by replaying the very command a user's own submit
/// would have sent. Nothing writes to the expenses collection directly, so permission checks, amount
/// validation, label creation and participant notifications all keep working for generated expenses
/// without a second implementation to keep in step.
/// </summary>
public static class RecurringExpenseOccurrence
{
    public static async Task<Result<string>> Create(
        IMediator mediator,
        RecurringExpense template,
        DateTime occurredUtc,
        CancellationToken ct)
    {
        switch (template)
        {
            case GroupRecurringExpense t:
            {
                var result = await mediator.Send(
                    new CreateExpenseCommand
                    {
                        UserId = t.UserId,
                        GroupId = t.GroupId,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        Description = t.Description,
                        Occurred = occurredUtc,
                        Payments = t.Payments,
                        Shares = t.Shares,
                        Labels = t.Labels,
                        Location = t.Location,
                        RecurringExpenseId = t.Id
                    },
                    ct);

                return result.IsFailure ? Result.Failure<string>(result.Error) : result.Value.ExpenseId;
            }

            case NonGroupRecurringExpense t:
            {
                var result = await mediator.Send(
                    new CreateNonGroupExpenseCommand
                    {
                        UserId = t.UserId,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        Description = t.Description,
                        Occurred = occurredUtc,
                        Payments = t.Payments,
                        Shares = t.Shares,
                        Labels = t.Labels,
                        Location = t.Location,
                        RecurringExpenseId = t.Id
                    },
                    ct);

                return result.IsFailure ? Result.Failure<string>(result.Error) : result.Value.ExpenseId;
            }

            case PersonalRecurringExpense t:
            {
                var result = await mediator.Send(
                    new CreatePersonalExpenseCommand
                    {
                        UserId = t.UserId,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        Description = t.Description,
                        Occurred = occurredUtc,
                        Labels = t.Labels,
                        Location = t.Location,
                        RecurringExpenseId = t.Id
                    },
                    ct);

                return result.IsFailure ? Result.Failure<string>(result.Error) : result.Value.ExpenseId;
            }

            default:
                return Result.Failure<string>($"Unsupported recurring expense type '{template.GetType().Name}'");
        }
    }
}
