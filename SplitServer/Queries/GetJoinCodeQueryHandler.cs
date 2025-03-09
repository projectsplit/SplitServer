using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetJoinCodeQueryHandler : IRequestHandler<GetJoinCodeQuery, Result<GetJoinCodeResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;

    public GetJoinCodeQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IJoinCodesRepository joinCodesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _joinCodesRepository = joinCodesRepository;
    }

    public async Task<Result<GetJoinCodeResponse>> Handle(GetJoinCodeQuery query, CancellationToken ct)
    {
        var joinCodeMaybe = await _joinCodesRepository.GetById(query.Code, ct);

        if (joinCodeMaybe.HasNoValue)
        {
            return Result.Failure<GetJoinCodeResponse>($"Code {query.Code} was not found");
        }

        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetJoinCodeResponse>($"User with id {query.UserId} was not found");
        }

        var joinCode = joinCodeMaybe.Value;

        var groupMaybe = await _groupsRepository.GetById(joinCode.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetJoinCodeResponse>($"Group with id {joinCode.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        return new GetJoinCodeResponse
        {
            IsAlreadyMember = group.Members.Any(x => x.UserId == query.UserId),
            GroupId = group.Id,
            GroupName = group.Name,
            IsExpired = joinCode.Expires < DateTime.UtcNow || joinCode.TimesUsed >= joinCode.MaxUses,
        };
    }
}