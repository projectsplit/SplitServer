using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchAllUsersQueryHandler : IRequestHandler<SearchAllUsersQuery, Result<SearchAllUsersResponse>>
{
    private readonly IUsersRepository _usersRepository;

    public SearchAllUsersQueryHandler(
        IUsersRepository usersRepository)
    {
        _usersRepository = usersRepository;
    }

    public async Task<Result<SearchAllUsersResponse>> Handle(SearchAllUsersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<SearchAllUsersResponse>($"User with id {query.UserId} was not found");
        }

        var skip = Next.Parse<SkipNext>(query.Next)?.Skip ?? 0;

        var users = query.Keyword is null || query.Keyword.Length < 2
            ? await _usersRepository.GetLatestUsers(skip, query.PageSize, ct)
            : await _usersRepository.SearchByUsername(query.Keyword, skip, query.PageSize, ct);

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
            Next = Next.Create(users, query.PageSize, _ => new SkipNext { Skip = skip + query.PageSize })
        };
    }
}

file class SkipNext
{
    public int Skip { get; init; }
}