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

        var page = await TransactionPages.Load<GroupTransfer>(
            query.PageSize,
            Next.Parse<NextTransferPageDetails>(query.Next),
            Next.Parse<NextTransferPageDetails>(query.Previous),
            (limit, occurred, created, direction, inclusive) =>
                _transfersRepository.GetByGroupId(query.GroupId, limit, occurred, created, direction, inclusive, ct));

        var transfers = page.Items;

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
            Next = page.HasMoreOlder ? CreateToken(transfers.Last()) : null,
            Previous = page.HasMoreNewer ? CreateToken(transfers.First()) : null
        };
    }

    private static string CreateToken(Transfer transfer) =>
        Next.Encode(new NextTransferPageDetails { Created = transfer.Created, Occurred = transfer.Occurred });
}