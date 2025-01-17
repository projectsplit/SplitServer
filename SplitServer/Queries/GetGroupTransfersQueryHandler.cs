using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetGroupTransfersQueryHandler : IRequestHandler<GetGroupTransfersQuery, Result<GetGroupTransfersResponse>>
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

    public async Task<Result<GetGroupTransfersResponse>> Handle(GetGroupTransfersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupTransfersResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupTransfersResponse>($"Group with id {query.GroupId} was not found");
        }
        
        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupTransfersResponse>("User must be a group member");
        }

        var nextDetails = ParseNext(query.Next);

        var transfers = await _transfersRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Occured,
            nextDetails?.Created,
            ct);

        return new GetGroupTransfersResponse
        {
            Transfers = transfers,
            Next = CreateNext(transfers, query.PageSize)
        };
    }

    private static string? CreateNext(List<Transfer> transfers, int pageSize)
    {
        if (transfers.Count < pageSize)
        {
            return default;
        }
        
        var lastTransferInPage = transfers.Last();

        var newNextDetails = new NextTransferPageDetails
        {
            Created = lastTransferInPage.Created,
            Occured = lastTransferInPage.Occured
        };
        
        var jsonString = JsonSerializer.Serialize(newNextDetails);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
    }

    private static NextTransferPageDetails? ParseNext(string? next)
    {
        if (string.IsNullOrEmpty(next))
        {
            return default;
        }

        var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(next));
        
        return JsonSerializer.Deserialize<NextTransferPageDetails>(jsonString);
    }
}

internal class NextTransferPageDetails
{
    public required DateTime Created { get; init; }
    
    public required DateTime Occured { get; init; }
}