using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchByGroupNameQueryHandler : IRequestHandler<SearchByGroupNameQuery, Result<GetGroupsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public SearchByGroupNameQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetGroupsResponse>> Handle(SearchByGroupNameQuery query, CancellationToken ct)
    {
        if (query.PageSize < 1)
        {
            return Result.Failure<GetGroupsResponse>("Page size must be greater than 0");
        }

        var nextDetails = Next.Parse<NextGroupPageDetails>(query.Next);
        
        var skip = Next.Parse<SkipNext>(query.Next)?.Skip ?? 0;
        var groups = query.Keyword is null || query.Keyword.Length < 2
            ? await _groupsRepository.GetByUserId(query.UserId, null, query.PageSize, nextDetails?.Created, ct)
            : await _groupsRepository.SearchByGroupName(query.UserId,query.Keyword, skip, query.PageSize, ct);

        var allMemberUserIds = groups.SelectMany(g => g.Members.Select(m => m.UserId)).Distinct().ToList();
        var users = await _usersRepository.GetByIds(allMemberUserIds, ct);
        var usersById = users.ToDictionary(u => u.Id);
        
        return new GetGroupsResponse
        {
            Groups = groups.Select(
                x => new GetGroupsResponseItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    OwnerId=x.OwnerId,
                    Currency = x.Currency,
                    IsArchived = x.IsArchived,
                    Guests = x.Guests,
                    Labels = x.Labels,
                    Members = x.Members.Select(
                        member => new GetGroupResponseMemberItem
                        {
                            Id = member.Id,
                            UserId = member.UserId,
                            Name = usersById.GetValueOrDefault(member.UserId)?.Username ?? DeletedUser.Username(member.UserId),
                            Joined = member.Joined
                        }).ToList(),
                    Created = x.Created,
                    Updated = x.Updated
                    
                }).ToList(),
            Next = Next.Create(groups, query.PageSize, _ => new SkipNext { Skip = skip + query.PageSize })
        };
    }
}

file class NextGroupPageDetails
{
    public required DateTime Created { get; init; }
}

file class SkipNext
{
    public int Skip { get; init; }
}