using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditNonGroupExpenseCommandHandler : IRequestHandler<EditNonGroupExpenseCommand, Result>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;
    private readonly ConnectionService _connectionService;

    public EditNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        IUsersRepository usersRepository,
        PermissionService permissionService,
        ValidationService validationService,
        UserLabelService userLabelService,
        ConnectionService connectionService)
    {
        _expensesRepository = expensesRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _userLabelService = userLabelService;
        _connectionService = connectionService;
    }

    public async Task<Result> Handle(EditNonGroupExpenseCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyNonGroupExpenseAction(command.UserId, command.ExpenseId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, nonGroupExpense) = permissionResult.Value;

        var expenseValidationResult = _validationService.ValidateNonGroupExpense(
            command.Payments,
            command.Shares,
            command.Amount,
            command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return expenseValidationResult;
        }

        var participantUserIds = command.Payments.Select(x => x.UserId)
            .Concat(command.Shares.Select(x => x.UserId))
            .ToList();

        var notConnectedUserIds = await _connectionService.GetNotConnectedUserIds(command.UserId, participantUserIds, ct);

        if (notConnectedUserIds.Count > 0)
        {
            var notConnectedUsers = await _usersRepository.GetByIds(notConnectedUserIds, ct);
            var usernames = string.Join(", ", notConnectedUsers.Select(x => x.Username));

            return Result.Failure($"You are not connected with: {usernames}. Send them a connection request first.");
        }

        var now = DateTime.UtcNow;

        var addLabelsToUserResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToUserResult.IsFailure)
        {
            return addLabelsToUserResult;
        }

        var editedExpense = nonGroupExpense with
        {
            Updated = now,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location
        };

        return await _expensesRepository.Update(editedExpense, ct);
    }
}