using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class DeclineConnectionRequestCommandHandler : IRequestHandler<DeclineConnectionRequestCommand, Result>
{
    private readonly IUserConnectionsRepository _userConnectionsRepository;

    public DeclineConnectionRequestCommandHandler(IUserConnectionsRepository userConnectionsRepository)
    {
        _userConnectionsRepository = userConnectionsRepository;
    }

    public async Task<Result> Handle(DeclineConnectionRequestCommand command, CancellationToken ct)
    {
        var connectionMaybe = await _userConnectionsRepository.GetById(command.ConnectionId, ct);

        // Already gone is the state the caller wanted, and declining a request twice is a normal
        // thing to end up doing from a stale menu.
        if (connectionMaybe.HasNoValue)
        {
            return Result.Success();
        }

        var connection = connectionMaybe.Value;

        if (connection.ReceiverId != command.UserId)
        {
            return Result.Failure("Only the receiver can decline a connection request");
        }

        if (connection.Status == ConnectionStatus.Accepted)
        {
            return Result.Failure("Connection request has already been accepted");
        }

        // Deleted rather than marked declined, so the sender can try again later.
        return await _userConnectionsRepository.Delete(connection.Id, ct);
    }
}
