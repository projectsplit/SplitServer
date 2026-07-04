using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

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
                    var connection = connections.FirstOrDefault(
                        x => x.SenderId == otherUserId || x.ReceiverId == otherUserId);

                    var status = connectedUserIds.Contains(otherUserId)
                        ? ConnectionStatusValues.Connected
                        : connection is null || connection.Status == ConnectionStatus.Accepted
                            ? ConnectionStatusValues.None
                            : connection.SenderId == query.UserId
                                ? ConnectionStatusValues.PendingSent
                                : ConnectionStatusValues.PendingReceived;

                    return new ConnectionStatusResponseItem
                    {
                        UserId = otherUserId,
                        Status = status,
                        ConnectionId = connection?.Id,
                    };
                })
            .ToList();

        return new GetConnectionStatusesResponse
        {
            Statuses = statuses,
        };
    }
}
