using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateNonGroupExpenseCommandHandler : IRequestHandler<CreateNonGroupExpenseCommand, Result<CreateExpenseResponse>>
{
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly UserLabelService _userLabelService;
    private readonly ConnectionService _connectionService;
    private readonly PushNotificationService _pushNotificationService;

    public CreateNonGroupExpenseCommandHandler(
        IExpensesRepository expensesRepository,
        IUsersRepository usersRepository,
        PermissionService permissionService,
        ValidationService validationService,
        UserLabelService userLabelService,
        ConnectionService connectionService,
        PushNotificationService pushNotificationService)
    {
        _expensesRepository = expensesRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _userLabelService = userLabelService;
        _connectionService = connectionService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result<CreateExpenseResponse>> Handle(CreateNonGroupExpenseCommand command, CancellationToken ct)
    {
        var expenseValidationResult = _validationService.ValidateNonGroupExpense(
            command.Payments,
            command.Shares,
            command.Amount,
            command.Currency);

        if (expenseValidationResult.IsFailure)
        {
            return Result.Failure<CreateExpenseResponse>(expenseValidationResult.Error);
        }

        var participantUserIds = command.Payments.Select(x => x.UserId)
            .Concat(command.Shares.Select(x => x.UserId))
            .ToList();

        var notConnectedUserIds = await _connectionService.GetNotConnectedUserIds(command.UserId, participantUserIds, ct);

        if (notConnectedUserIds.Count > 0)
        {
            var notConnectedUsers = await _usersRepository.GetByIds(notConnectedUserIds, ct);
            var usernames = string.Join(", ", notConnectedUsers.Select(x => x.Username));

            return Result.Failure<CreateExpenseResponse>(
                $"You are not connected with: {usernames}. Send them a connection request first.");
        }

        var now = DateTime.UtcNow;

        var addLabelsToGroupResult = await _userLabelService.AddUserLabelsIfMissing(command.UserId, command.Labels, now, ct);

        if (addLabelsToGroupResult.IsFailure)
        {
            return addLabelsToGroupResult.ConvertFailure<CreateExpenseResponse>();
        }

        var expenseId = Guid.NewGuid().ToString();

        var newNonGroupExpense = new NonGroupExpense
        {
            Id = expenseId,
            Created = now,
            Updated = now,
            CreatorId = command.UserId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency,
            Payments = command.Payments,
            Shares = command.Shares,
            Labels = command.Labels.Select(x => x.Text).ToList(),
            Location = command.Location
        };

        var writeResult = await _expensesRepository.Insert(newNonGroupExpense, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateExpenseResponse>();
        }

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        _pushNotificationService.NotifyInBackground(
            participantUserIds.Where(x => x != command.UserId),
            "New expense",
            $"{creatorUsername} added \"{command.Description}\" ({command.Amount} {command.Currency}) with you.",
            "/shared/nongroup/expenses");

        return new CreateExpenseResponse
        {
            ExpenseId = expenseId
        };
    }
}
