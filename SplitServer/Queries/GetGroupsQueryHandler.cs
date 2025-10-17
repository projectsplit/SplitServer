using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, Result<GetGroupsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public GetGroupsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetGroupsResponse>> Handle(GetGroupsQuery query, CancellationToken ct)
    {
        if (query.PageSize < 1)
        {
            return Result.Failure<GetGroupsResponse>("Page size must be greater than 0");
        }

        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<NextGroupPageDetails>(query.Next);

        var groups = await _groupsRepository.GetByUserId(query.UserId, null, query.PageSize, nextDetails?.Created, ct);

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
                    Members = x.Members,
                    Created = x.Created,
                    Updated = x.Updated
                    
                }).ToList(),
            Next = GetNext(query, groups)
        };
    }

    private static string? GetNext(GetGroupsQuery query, List<Group> groups)
    {
        return Next.Create(groups, query.PageSize, x => new NextGroupPageDetails { Created = x.Last().Created });
    }
}

file class NextGroupPageDetails
{
    public required DateTime Created { get; init; }
}