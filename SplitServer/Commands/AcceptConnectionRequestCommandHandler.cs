using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class AcceptConnectionRequestCommandHandler : IRequestHandler<AcceptConnectionRequestCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly PushNotificationService _pushNotificationService;

    public AcceptConnectionRequestCommandHandler(
        IUsersRepository usersRepository,
        IUserConnectionsRepository userConnectionsRepository,
        PushNotificationService pushNotificationService)
    {
        _usersRepository = usersRepository;
        _userConnectionsRepository = userConnectionsRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result> Handle(AcceptConnectionRequestCommand command, CancellationToken ct)
    {
        var connectionMaybe = await _userConnectionsRepository.GetById(command.ConnectionId, ct);

        if (connectionMaybe.HasNoValue)
        {
            return Result.Failure($"Connection request with id {command.ConnectionId} was not found");
        }

        var connection = connectionMaybe.Value;

        if (connection.ReceiverId != command.UserId)
        {
            return Result.Failure("Only the receiver can accept a connection request");
        }

        if (connection.Status == ConnectionStatus.Accepted)
        {
            return Result.Success();
        }

        var acceptedConnection = connection with
        {
            Status = ConnectionStatus.Accepted,
            Updated = DateTime.UtcNow
        };

        var updateResult = await _userConnectionsRepository.Update(acceptedConnection, ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var receiverMaybe = await _usersRepository.GetById(command.UserId, ct);

        var receiverUsername = receiverMaybe.HasValue ? receiverMaybe.Value.Username : "Someone";

        _pushNotificationService.NotifyInBackground(
            [connection.SenderId],
            "Connection accepted",
            $"{receiverUsername} accepted your request. You can now split expenses together.");

        return Result.Success();
    }
}
