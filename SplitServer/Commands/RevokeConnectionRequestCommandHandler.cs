using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

/// <summary>
/// Takes back a request the user sent. The mirror of declining: same row, removed by the other
/// side of it.
/// </summary>
public class RevokeConnectionRequestCommandHandler : IRequestHandler<RevokeConnectionRequestCommand, Result>
{
    private readonly IUserConnectionsRepository _userConnectionsRepository;

    public RevokeConnectionRequestCommandHandler(IUserConnectionsRepository userConnectionsRepository)
    {
        _userConnectionsRepository = userConnectionsRepository;
    }

    public async Task<Result> Handle(RevokeConnectionRequestCommand command, CancellationToken ct)
    {
        var connectionMaybe = await _userConnectionsRepository.GetById(command.ConnectionId, ct);

        // Already gone is the state the caller wanted. The receiver may well have declined it in
        // the meantime, which is not something to report as a failure.
        if (connectionMaybe.HasNoValue)
        {
            return Result.Success();
        }

        var connection = connectionMaybe.Value;

        if (connection.SenderId != command.UserId)
        {
            return Result.Failure("Only the sender can revoke a connection request");
        }

        // Revoking cannot undo an acceptance: by then the two are connected and may already share
        // expenses, so pulling the link out from under them would be a different, destructive act.
        if (connection.Status == ConnectionStatus.Accepted)
        {
            return Result.Failure("Connection request has already been accepted");
        }

        // Deleted rather than marked revoked, so either side can start again later.
        return await _userConnectionsRepository.Delete(connection.Id, ct);
    }
}
