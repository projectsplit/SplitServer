using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupQueryHandler : IRequestHandler<GetGroupQuery, Result<GetGroupResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetGroupQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
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
            IsArchived = group.IsArchived,
            Members = group.Members.Select(
                x => new GetGroupResponseMemberItem
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Name = usersById.GetValueOrDefault(x.UserId)?.Username ?? "deleted-user",
                    Joined = x.Joined
                }).ToList(),
            Guests = await GetGuestResponseItems(
                group.Id,
                group.Guests,
                ct),
            Labels = group.Labels,
        };
    }

    private async Task<List<GetGroupResponseGuestItem>> GetGuestResponseItems(string groupId, List<Guest> guests, CancellationToken ct)
    {
        var guestResponseItems = new List<GetGroupResponseGuestItem>();

        foreach (var guest in guests)
        {
            var existsInExpense = await _expensesRepository.ExistsInAnyExpense(groupId, guest.Id, ct);
            var existsInTransfer = await _transfersRepository.ExistsInAnyTransfer(groupId, guest.Id, ct);

            guestResponseItems.Add(
                new GetGroupResponseGuestItem
                {
                    Id = guest.Id,
                    Name = guest.Name,
                    Joined = guest.Joined,
                    CanBeRemoved = !existsInExpense && !existsInTransfer,
                });
        }

        return guestResponseItems;
    }
}