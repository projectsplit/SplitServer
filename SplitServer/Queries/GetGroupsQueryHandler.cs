using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

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
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = ParseNext(query.Next);

        var groups = await _groupsRepository.GetByUserId(query.UserId, query.PageSize, nextDetails?.Created, ct);

        return new GetGroupsResponse
        {
            Groups = groups.Select(x => new GetGroupsResponseItem
            {
                Id = x.Id,
                Name = x.Name
            }).ToList(),
            Next = CreateNext(groups, query.PageSize)
        };
    }

    private static string? CreateNext(List<Group> groups, int pageSize)
    {
        if (groups.Count < pageSize)
        {
            return default;
        }
        
        var lastTransferInPage = groups.Last();

        var newNextDetails = new NextGroupPageDetails
        {
            Created = lastTransferInPage.Created
        };
        
        var jsonString = JsonSerializer.Serialize(newNextDetails);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
    }

    private static NextGroupPageDetails? ParseNext(string? next)
    {
        if (string.IsNullOrEmpty(next))
        {
            return default;
        }

        var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(next));
        
        return JsonSerializer.Deserialize<NextGroupPageDetails>(jsonString);
    }
}

internal class NextGroupPageDetails
{
    public required DateTime Created { get; init; }
}