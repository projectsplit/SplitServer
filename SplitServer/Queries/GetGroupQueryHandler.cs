using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupQueryHandler : IRequestHandler<GetGroupQuery, Result<GetGroupResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public GetGroupQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetGroupResponse>> Handle(GetGroupQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupResponse>("User must be a group member");
        }

        var memberUserIds = group.Members.Select(x => x.UserId).ToList();

        var users = await _usersRepository.GetByIds(memberUserIds, ct);

        var usersById = users.ToDictionary(x => x.Id);

        return new GetGroupResponse
        {
            Id = group.Id,
            IsDeleted = false,
            Created = group.Created,
            Updated = group.Updated,
            OwnerId = group.OwnerId,
            Name = group.Name,
            Currency = group.Currency,
            Members = group.Members.Select(
                x => new GetGroupResponseMemberItem
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Name = usersById.GetValueOrDefault(x.UserId)?.Username ?? "N/A",
                    Joined = x.Joined
                }).ToList(),
            Guests = group.Guests,
            Labels = group.Labels
        };
    }
}