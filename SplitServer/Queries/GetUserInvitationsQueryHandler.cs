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
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetUserInvitationsQueryHandler(
        IUsersRepository usersRepository,
        IInvitationsRepository invitationsRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _invitationsRepository = invitationsRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
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

        var groupsById = groups.ToDictionary(x => x.Id);

        // A guest's balance is what accepting takes on, so it is shown with the invitation. It
        // needs the group's whole ledger, read once per group that has a guest waiting.
        var ledgersByGroupId = new Dictionary<string, (List<GroupExpense> Expenses, List<GroupTransfer> Transfers)>();

        foreach (var groupId in invitations.Where(x => x.GuestId is not null).Select(x => x.GroupId).Distinct())
        {
            ledgersByGroupId[groupId] = (
                await _expensesRepository.GetGroupExpensesByGroupId(groupId, ct),
                await _transfersRepository.GetAllByGroupId(groupId, ct));
        }

        var responseItems = invitations
            .Select(x =>
            {
                var group = groupsById[x.GroupId];
                var guest = group.Guests.FirstOrDefault(g => g.Id == x.GuestId);

                return new InvitationResponseItem
                {
                    Id = x.Id,
                    Created = x.Created,
                    SenderId = x.SenderId,
                    ReceiverId = x.ReceiverId,
                    GroupId = x.GroupId,
                    GroupName = group.Name,
                    GuestId = x.GuestId,
                    GuestName = guest?.Name,
                    GuestBalance = guest is null
                        ? null
                        : GroupService.GetBalance(guest.Id, ledgersByGroupId[x.GroupId].Expenses, ledgersByGroupId[x.GroupId].Transfers),
                    IsMerge = x.GuestId is not null && group.Members.Any(m => m.UserId == x.ReceiverId),
                };
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