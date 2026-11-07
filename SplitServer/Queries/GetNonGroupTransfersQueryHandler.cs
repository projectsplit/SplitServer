using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetNonGroupTransfersQueryHandler : IRequestHandler<GetNonGroupTransfersQuery, Result<NonGroupTransfersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITransfersRepository _transfersRepository;

    public GetNonGroupTransfersQueryHandler(
        IUsersRepository usersRepository,
        ITransfersRepository transfersRepository)
    {
        _usersRepository = usersRepository;
        _transfersRepository = transfersRepository;
    }

    public async Task<Result<NonGroupTransfersResponse>> Handle(GetNonGroupTransfersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<NonGroupTransfersResponse>($"User with id {query.UserId} was not found");
        }

        var page = await TransactionPages.Load<NonGroupTransfer>(
            query.PageSize,
            Next.Parse<NextTransferPageDetails>(query.Next),
            Next.Parse<NextTransferPageDetails>(query.Previous),
            (limit, occurred, created, direction, inclusive) =>
                _transfersRepository.GetByUserId(query.UserId, limit, occurred, created, direction, inclusive, ct));

        var transfers = page.Items;

        return new NonGroupTransfersResponse
        {
            Transfers = transfers
                .Select(x => new NonGroupTransferResponseItem
                {
                    Id = x.Id,
                    Created = x.Created,
                    Updated = x.Updated,
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