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
    private readonly IExpensesRepository _expensesRepository;

    public DeleteRecurringExpenseCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        IExpensesRepository expensesRepository)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _expensesRepository = expensesRepository;
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

        var deleteResult = await _recurringExpensesRepository.Delete(command.RecurringExpenseId, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        // Done after the delete rather than before: if the delete fails the expenses keep pointing
        // at a schedule that is still there, which is the truthful state.
        return await _expensesRepository.ClearRecurringExpenseId(command.RecurringExpenseId, ct);
    }
}
