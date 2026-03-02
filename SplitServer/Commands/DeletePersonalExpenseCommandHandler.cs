using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;


public class DeletePersonalExpenseCommandHandler : IRequestHandler<DeletePersonalExpenseCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IExpensesRepository _expensesRepository;

    public DeletePersonalExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        PermissionService permissionService)
    {
        _expensesRepository = expensesRepository;
        _permissionService = permissionService;
    }

    public async Task<Result> Handle(DeletePersonalExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyPersonalExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        return await _expensesRepository.Delete(command.ExpenseId, ct);
    }
}