using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class AcceptConnectionRequestCommandHandler : IRequestHandler<AcceptConnectionRequestCommand, Result>
{
    private const string NonGroupExpensesUrl = "/shared/nongroup/expenses";

    private readonly IUsersRepository _usersRepository;
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly NotificationService _notificationService;

    public AcceptConnectionRequestCommandHandler(
        IUsersRepository usersRepository,
        IUserConnectionsRepository userConnectionsRepository,
        NotificationService notificationService)
    {
        _usersRepository = usersRepository;
        _userConnectionsRepository = userConnectionsRepository;
        _notificationService = notificationService;
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

        // Accepting twice is what a double tap or a stale menu looks like, and the end state is
        // the one that was asked for either way.
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

        // The pending request the sender could see is gone now, so this is the only record left
        // that tells them the answer.
        await _notificationService.Notify(
            [connection.SenderId],
            "Connection accepted",
            $"{receiverUsername} accepted your request. You can now split expenses together.",
            NonGroupExpensesUrl,
            ct);

        return Result.Success();
    }
}
