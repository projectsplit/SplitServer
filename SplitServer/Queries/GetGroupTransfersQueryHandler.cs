using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupTransfersQueryHandler : IRequestHandler<GetGroupTransfersQuery, Result<GroupTransfersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetGroupTransfersQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result<GroupTransfersResponse>> Handle(GetGroupTransfersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GroupTransfersResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GroupTransfersResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GroupTransfersResponse>("User must be a group member");
        }

        var nextDetails = Next.Parse<NextTransferPageDetails>(query.Next);

        var transfers = await _transfersRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        return new GroupTransfersResponse
        {
            Transfers = transfers
                .Select(x => new GroupTransferResponseItem
                {
                    Id = x.Id,
                    Created = x.Created,
                    Updated = x.Updated,
                    GroupId = x.GroupId,
                    CreatorId = x.CreatorId,
                    Amount = x.Amount,
                    Occurred = x.Occurred,
                    Description = x.Description,
                    Currency = x.Currency,
                    ReceiverId = x.ReceiverId,
                    SenderId = x.SenderId,
                })
                .ToList(),
            Next = GetNext(query, transfers)
        };
    }

    private static string? GetNext(GetGroupTransfersQuery query, List<GroupTransfer> transfers)
    {
        return Next.Create(
            transfers,
            query.PageSize,
            x => new NextTransferPageDetails
            {
                Created = x.Last().Created,
                Occurred = x.Last().Occurred
            });
    }
}