using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchAllUsersQueryHandler : IRequestHandler<SearchAllUsersQuery, Result<SearchAllUsersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ConnectionService _connectionService;

    public SearchAllUsersQueryHandler(
        IUsersRepository usersRepository,
        ConnectionService connectionService)
    {
        _usersRepository = usersRepository;
        _connectionService = connectionService;
    }

    public async Task<Result<SearchAllUsersResponse>> Handle(SearchAllUsersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<SearchAllUsersResponse>($"User with id {query.UserId} was not found");
        }

        var position = Next.Parse<SearchPosition>(query.Next) ?? new SearchPosition();

        // Connected the way the statuses endpoint means it, which the picker sorts its sections by:
        // an accepted request, a shared group, or splits together before. Putting only accepted
        // requests first left people known from a group to turn up pages down the list, where the
        // picker's Connections section never showed them until they were scrolled to.
        var connectedUserIds = (await _connectionService.GetConnectedUserIds(query.UserId, ct)).ToList();

        // People already connected to fill the page first, and whatever room is left over goes to
        // everyone else. Topping the page up here rather than letting the connections run out on a
        // page of their own is what keeps paging alive: a page shorter than asked for is how this
        // API says "that was the last of them", so a half-empty final page of connections would
        // end the results before a single other user had been reached.
        var connected = await Search(query, UserIdScope.Only(connectedUserIds), position.ConnectedSkip, query.PageSize, ct);

        var rest = connected.Count < query.PageSize
            ? await Search(
                query,
                UserIdScope.Except(connectedUserIds),
                position.RestSkip,
                query.PageSize - connected.Count,
                ct)
            : [];

        var users = connected.Concat(rest).ToList();

        return new SearchAllUsersResponse
        {
            Users = users
                .Select(
                    x => new SearchUsersResponseItem
                    {
                        UserId = x.Id,
                        Username = x.Username,
                    })
                .ToList(),
            Next = Next.Create(
                users,
                query.PageSize,
                _ => new SearchPosition
                {
                    ConnectedSkip = position.ConnectedSkip + connected.Count,
                    RestSkip = position.RestSkip + rest.Count
                })
        };
    }

    private async Task<List<User>> Search(
        SearchAllUsersQuery query,
        UserIdScope scope,
        int skip,
        int take,
        CancellationToken ct)
    {
        return query.Keyword is null || query.Keyword.Length < 2
            ? await _usersRepository.GetLatestUsers(scope, skip, take, ct)
            : await _usersRepository.SearchByUsername(query.Keyword, scope, skip, take, ct);
    }
}

/// <summary>
/// The two halves of the list are paged independently, so the cursor has to carry a position in
/// each: a single offset cannot say how far into the connections a page that spans both ended up.
/// </summary>
file class SearchPosition
{
    public int ConnectedSkip { get; init; }
    public int RestSkip { get; init; }
}
