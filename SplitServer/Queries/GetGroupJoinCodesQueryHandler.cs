using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupJoinCodesQueryHandler : IRequestHandler<GetGroupJoinCodesQuery, Result<GetGroupJoinCodesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;

    public GetGroupJoinCodesQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IJoinCodesRepository joinCodesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _joinCodesRepository = joinCodesRepository;
    }

    public async Task<Result<GetGroupJoinCodesResponse>> Handle(GetGroupJoinCodesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupJoinCodesResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupJoinCodesResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupJoinCodesResponse>("User must be a group member");
        }

        var nextDetails = Next.Parse<GroupJoinCodesNextDetails>(query.Next);

        var joinCodes = await _joinCodesRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Created,
            ct);

        return new GetGroupJoinCodesResponse
        {
            Codes = joinCodes,
            Next = GetNext(query.PageSize, joinCodes),
        };
    }

    private static string? GetNext(int pageSize, List<JoinCode> joinCodes)
    {
        return Next.Create(
            joinCodes,
            pageSize,
            x => new GroupJoinCodesNextDetails { Created = x.Last().Created });
    }
}

file class GroupJoinCodesNextDetails
{
    public required DateTime Created { get; init; }
}