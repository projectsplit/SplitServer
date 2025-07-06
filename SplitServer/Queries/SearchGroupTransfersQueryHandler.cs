using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchGroupTransfersQueryHandler : IRequestHandler<SearchGroupTransfersQuery, Result<GroupTransfersResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public SearchGroupTransfersQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        ITransfersRepository transfersRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _transfersRepository = transfersRepository;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result<GroupTransfersResponse>> Handle(SearchGroupTransfersQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GroupTransfersResponse>($"User with id {query.UserId} was not found");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

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

        var transfers = await _transfersRepository.Search(
            query.GroupId,
            query.SearchTerm,
            query.Before?.ToUtc(userTimeZoneId),
            query.After?.ToUtc(userTimeZoneId),
            query.ReceiverIds,
            query.SenderIds,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        return new GroupTransfersResponse
        {
            Transfers = transfers,
            Next = GetNext(query, transfers)
        };
    }

    private static string? GetNext(SearchGroupTransfersQuery query, List<Transfer> transfers)
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