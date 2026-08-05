using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

/// <summary>
/// Stops the schedule. Expenses already produced are left alone — they are real spending that
/// happened, and deleting them would silently rewrite balances other people rely on.
/// </summary>
public class DeleteRecurringExpenseCommandHandler : IRequestHandler<DeleteRecurringExpenseCommand, Result>
{
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;

    public DeleteRecurringExpenseCommandHandler(IRecurringExpensesRepository recurringExpensesRepository)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
    }

    public async Task<Result> Handle(DeleteRecurringExpenseCommand command, CancellationToken ct)
    {
        var templateMaybe = await _recurringExpensesRepository.GetById(command.RecurringExpenseId, ct);

        if (templateMaybe.HasNoValue)
        {
            return Result.Failure($"Recurring expense with id {command.RecurringExpenseId} was not found");
        }

        if (templateMaybe.Value.UserId != command.UserId)
        {
            return Result.Failure("This recurring expense does not belong to user");
        }

        return await _recurringExpensesRepository.Delete(command.RecurringExpenseId, ct);
    }
}
