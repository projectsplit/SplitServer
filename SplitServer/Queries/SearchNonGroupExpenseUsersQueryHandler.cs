using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchNonGroupExpenseUsersQueryHandler : IRequestHandler<SearchNonGroupExpenseUsersQuery, Result<SearchNonGroupUsersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;

    public SearchNonGroupExpenseUsersQueryHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result<SearchNonGroupUsersResponse>> Handle(SearchNonGroupExpenseUsersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<SearchNonGroupUsersResponse>($"User with id {query.UserId} was not found");
        }

        var userIds = await _expensesRepository.GetNonGroupUserIdsByUserId(query.UserId, ct);

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