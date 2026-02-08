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

        var nextDetails = Next.Parse<NextTransferPageDetails>(query.Next);

        var transfers = await _transfersRepository.GetByUserId(
            query.UserId,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

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
            Next = GetNext(query, transfers)
        };
    }

    private static string? GetNext(GetNonGroupTransfersQuery query, List<NonGroupTransfer> transfers)
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