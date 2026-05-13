using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchUsersToInviteQueryHandler : IRequestHandler<SearchUsersToInviteQuery, Result<SearchUsersToInviteResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IInvitationsRepository _invitationsRepository;

    public SearchUsersToInviteQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IInvitationsRepository invitationsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _invitationsRepository = invitationsRepository;
    }

    public async Task<Result<SearchUsersToInviteResponse>> Handle(SearchUsersToInviteQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<SearchUsersToInviteResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<SearchUsersToInviteResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        var skip = Next.Parse<SkipNext>(query.Next)?.Skip ?? 0;

        var users = query.Keyword is null || query.Keyword.Length < 2
            ? await _usersRepository.GetLatestUsers(skip, query.PageSize, ct)
            : await _usersRepository.SearchByUsername(query.Keyword, skip, query.PageSize, ct);

        var invitations = await _invitationsRepository.GetByReceiverIds(users.Select(x => x.Id).ToList(), query.GroupId, ct);

        return new SearchUsersToInviteResponse
        {
            Users = users
                .Select(
                    x => new SearchUsersToInviteResponseItem
                    {
                        UserId = x.Id,
                        Username = x.Username,
                        IsGroupMember = group.Members.Any(m => m.UserId == x.Id),
                        IsAlreadyInvited = invitations.Any(i => i.ReceiverId == x.Id)
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