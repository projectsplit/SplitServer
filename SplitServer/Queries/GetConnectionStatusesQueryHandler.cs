using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

/// <summary>
/// Tells the user pickers which of the users on screen can be split with, and for the rest
/// whether a request is already in flight and which way round it points.
/// </summary>
public class GetConnectionStatusesQueryHandler : IRequestHandler<GetConnectionStatusesQuery, Result<GetConnectionStatusesResponse>>
{
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly ConnectionService _connectionService;

    public GetConnectionStatusesQueryHandler(
        IUserConnectionsRepository userConnectionsRepository,
        ConnectionService connectionService)
    {
        _userConnectionsRepository = userConnectionsRepository;
        _connectionService = connectionService;
    }

    public async Task<Result<GetConnectionStatusesResponse>> Handle(GetConnectionStatusesQuery query, CancellationToken ct)
    {
        var otherUserIds = query.UserIds
            .Where(x => x != query.UserId)
            .Distinct()
            .ToList();

        if (otherUserIds.Count == 0)
        {
            return new GetConnectionStatusesResponse
            {
                Statuses = [],
            };
        }

        var connectedUserIds = await _connectionService.GetConnectedUserIds(query.UserId, ct);

        var connections = await _userConnectionsRepository.GetAllBetweenUsers(query.UserId, otherUserIds, ct);

        var statuses = otherUserIds
            .Select(
                otherUserId =>
                {
                    // Connected wins over any pending row: sharing a group or an expense already
                    // makes them selectable, whatever an outstanding request says.
                    if (connectedUserIds.Contains(otherUserId))
                    {
                        return new ConnectionStatusResponseItem
                        {
                            UserId = otherUserId,
                            Status = ConnectionStatusValues.Connected,
                            ConnectionId = null,
                        };
                    }

                    var connection = connections.FirstOrDefault(
                        x => x.SenderId == otherUserId || x.ReceiverId == otherUserId);

                    if (connection is null)
                    {
                        return new ConnectionStatusResponseItem
                        {
                            UserId = otherUserId,
                            Status = ConnectionStatusValues.None,
                            ConnectionId = null,
                        };
                    }

                    return new ConnectionStatusResponseItem
                    {
                        UserId = otherUserId,
                        Status = connection.SenderId == query.UserId
                            ? ConnectionStatusValues.PendingSent
                            : ConnectionStatusValues.PendingReceived,
                        ConnectionId = connection.Id,
                    };
                })
            .ToList();

        return new GetConnectionStatusesResponse
        {
            Statuses = statuses,
        };
    }
}
