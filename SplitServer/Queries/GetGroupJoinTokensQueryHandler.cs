using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupJoinTokensQueryHandler : IRequestHandler<GetGroupJoinTokensQuery, Result<GetGroupJoinTokensResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IJoinTokensRepository _joinTokensRepository;

    public GetGroupJoinTokensQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IJoinTokensRepository joinTokensRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _joinTokensRepository = joinTokensRepository;
    }

    public async Task<Result<GetGroupJoinTokensResponse>> Handle(GetGroupJoinTokensQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupJoinTokensResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupJoinTokensResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupJoinTokensResponse>("User must be a group member");
        }

        var nextDetails = Next.Parse<GroupJoinTokensNextDetails>(query.Next);

        var joinTokens = await _joinTokensRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Created,
            ct);

        return new GetGroupJoinTokensResponse
        {
            JoinTokens = joinTokens,
            Next = GetNext(query.PageSize, joinTokens),
        };
    }

    private static string? GetNext(int pageSize, List<JoinToken> joinTokens)
    {
        return Next.Create(
            joinTokens,
            pageSize,
            x => new GroupJoinTokensNextDetails { Created = x.Last().Created });
    }
}

file class GroupJoinTokensNextDetails
{
    public required DateTime Created { get; init; }
}