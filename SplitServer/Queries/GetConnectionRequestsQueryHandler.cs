using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetConnectionRequestsQueryHandler : IRequestHandler<GetConnectionRequestsQuery, Result<GetConnectionRequestsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserConnectionsRepository _userConnectionsRepository;

    public GetConnectionRequestsQueryHandler(
        IUsersRepository usersRepository,
        IUserConnectionsRepository userConnectionsRepository)
    {
        _usersRepository = usersRepository;
        _userConnectionsRepository = userConnectionsRepository;
    }

    public async Task<Result<GetConnectionRequestsResponse>> Handle(GetConnectionRequestsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetConnectionRequestsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<ConnectionRequestsNext>(query.Next);
        var maxCreatedDate = nextDetails?.MaxCreatedDate ?? DateTime.UtcNow;

        var connections = await _userConnectionsRepository.GetPendingByReceiverId(query.UserId, query.PageSize, maxCreatedDate, ct);

        var senders = await _usersRepository.GetByIds(connections.Select(x => x.SenderId).ToList(), ct);

        var sendersById = senders.ToDictionary(x => x.Id);

        var responseItems = connections
            .Select(x => new ConnectionRequestResponseItem
            {
                Id = x.Id,
                Created = x.Created,
                SenderId = x.SenderId,
                SenderUsername = sendersById.GetValueOrDefault(x.SenderId)?.Username ?? DeletedUser.Username(x.SenderId),
            })
            .ToList();

        return new GetConnectionRequestsResponse
        {
            ConnectionRequests = responseItems,
            Next = Next.Create(connections, query.PageSize, x => new ConnectionRequestsNext { MaxCreatedDate = x.Last().Created })
        };
    }
}

file class ConnectionRequestsNext
{
    public required DateTime MaxCreatedDate { get; init; }
}
