using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services;

public class PermissionService
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;

    public PermissionService(
        IGroupsRepository groupsRepository,
        IUsersRepository usersRepository)
    {
        _groupsRepository = groupsRepository;
        _usersRepository = usersRepository;
    }

    public async Task<Result<(User user, Group group, string memberId)>> VerifyGroupAction(
        string userId,
        string groupId,
        CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(userId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, string memberId)>($"User with id {userId} was not found");
        }

        var user = userMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(groupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<(User user, Group group, string memberId)>($"Group with id {groupId} was not found");
        }

        var group = groupMaybe.Value;

        var memberId = group.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

        if (memberId is null)
        {
            return Result.Failure<(User user, Group group, string memberId)>("User must be a group member");
        }

        return (user, group, memberId);
    }
}