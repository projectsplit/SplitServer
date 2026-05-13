using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchNonGroupTransferUsersQueryHandler : IRequestHandler<SearchNonGroupTransferUsersQuery, Result<SearchNonGroupUsersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITransfersRepository _transfersRepository;

    public SearchNonGroupTransferUsersQueryHandler(
        IUsersRepository usersRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result<SearchNonGroupUsersResponse>> Handle(SearchNonGroupTransferUsersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<SearchNonGroupUsersResponse>($"User with id {query.UserId} was not found");
        }

        var userIds = await _transfersRepository.GetNonGroupUserIdsByUserId(query.UserId, ct);

        var users = await _usersRepository.GetByIds(userIds, ct);

        var usersById = users.ToDictionary(x => x.Id);

        var orderedUsers = userIds
            .Select(x => usersById.GetValueOrDefault(x))
            .WhereNotNull()
            .ToList();

        return new SearchNonGroupUsersResponse
        {
            Users = orderedUsers
                .Select(x => new SearchUsersResponseItem
                {
                    UserId = x.Id,
                    Username = x.Username,
                })
                .ToList(),
        };
    }
}