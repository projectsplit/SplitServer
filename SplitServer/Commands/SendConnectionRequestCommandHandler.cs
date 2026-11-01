using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class SendConnectionRequestCommandHandler : IRequestHandler<SendConnectionRequestCommand, Result>
{
    private const string NonGroupExpensesUrl = "/shared/nongroup/expenses";

    private readonly IUsersRepository _usersRepository;
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly ConnectionService _connectionService;
    private readonly NotificationService _notificationService;
    private readonly PushNotificationService _pushNotificationService;

    public SendConnectionRequestCommandHandler(
        IUsersRepository usersRepository,
        IUserConnectionsRepository userConnectionsRepository,
        ConnectionService connectionService,
        NotificationService notificationService,
        PushNotificationService pushNotificationService)
    {
        _usersRepository = usersRepository;
        _userConnectionsRepository = userConnectionsRepository;
        _connectionService = connectionService;
        _notificationService = notificationService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result> Handle(SendConnectionRequestCommand command, CancellationToken ct)
    {
        if (command.UserId == command.ReceiverId)
        {
            return Result.Failure("You cannot send a connection request to yourself");
        }

        var senderMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (senderMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var receiverMaybe = await _usersRepository.GetById(command.ReceiverId, ct);

        if (receiverMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.ReceiverId} was not found");
        }

        var connectedUserIds = await _connectionService.GetConnectedUserIds(command.UserId, ct);

        if (connectedUserIds.Contains(command.ReceiverId))
        {
            return Result.Failure("You can already split expenses with this user");
        }

        var existingConnectionMaybe = await _userConnectionsRepository.GetBetweenUsers(command.UserId, command.ReceiverId, ct);

        var now = DateTime.UtcNow;
        var senderUsername = senderMaybe.Value.Username;

        if (existingConnectionMaybe.HasValue)
        {
            var existingConnection = existingConnectionMaybe.Value;

            if (existingConnection.SenderId == command.UserId)
            {
                return Result.Failure("You have already sent a connection request to this user");
            }

            // They asked first and this user answered by asking back, which says yes just as
            // clearly as pressing accept would have.
            var acceptedConnection = existingConnection with
            {
                Status = ConnectionStatus.Accepted,
                Updated = now
            };

            var updateResult = await _userConnectionsRepository.Update(acceptedConnection, ct);

            if (updateResult.IsFailure)
            {
                return updateResult;
            }

            // Their pending request is gone now, just as if this user had pressed accept, so they get
            // the same lasting record of the answer rather than a push that leaves nothing behind.
            await _notificationService.Notify(
                [existingConnection.SenderId],
                "Connection accepted",
                $"{senderUsername} accepted your request. You can now split expenses together.",
                NonGroupExpensesUrl,
                ct);

            return Result.Success();
        }

        var newConnection = new UserConnection
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            SenderId = command.UserId,
            ReceiverId = command.ReceiverId,
            Status = ConnectionStatus.Pending,
        };

        var insertResult = await _userConnectionsRepository.Insert(newConnection, ct);

        if (insertResult.IsFailure)
        {
            return insertResult;
        }

        // The pending request is its own entry in the receiver's notifications menu, so this only
        // needs to nudge them to look at it rather than leave a second record behind.
        _pushNotificationService.NotifyInBackground(
            [command.ReceiverId],
            "New connection request",
            $"{senderUsername} wants to split expenses with you.");

        return Result.Success();
    }
}
