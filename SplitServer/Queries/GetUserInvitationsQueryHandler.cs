using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetUserInvitationsQueryHandler : IRequestHandler<GetUserInvitationsQuery, Result<GetUserInvitationsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IGroupsRepository _groupsRepository;

    public GetUserInvitationsQueryHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository,
        IGroupsRepository groupsRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetUserInvitationsResponse>> Handle(GetUserInvitationsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUserInvitationsResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<UserInvitationsNext>(query.Next);
        var maxCreatedDate = nextDetails?.MaxCreatedDate ?? DateTime.UtcNow;

        var invitations = await _invitationsRepository.GetByReceiverId(query.UserId, query.PageSize, maxCreatedDate, ct);

        var groups = await _groupsRepository.GetByIds(invitations.Select(x => x.GroupId).ToList(), ct);

        var groupNames = groups.ToDictionary(x => x.Id, x => x.Name);

        var responseItems = invitations
            .Select(
                x => new InvitationResponseItem
                {
                    Id = x.Id,
                    Created = x.Created,
                    SenderId = x.SenderId,
                    ReceiverId = x.ReceiverId,
                    GroupId = x.GroupId,
                    GroupName = groupNames[x.GroupId],
                    GuestId = x.GuestId
                })
            .ToList();

        return new GetUserInvitationsResponse
        {
            Invitations = responseItems,
            Next = CreateNext(query.PageSize, invitations)
        };
    }

    private static string? CreateNext(int pageSize, List<Invitation> invitations)
    {
        return Next.Create(invitations, pageSize, x => new UserInvitationsNext { MaxCreatedDate = x.Last().Created });
    }
}

file class UserInvitationsNext
{
    public required DateTime MaxCreatedDate { get; init; }
}