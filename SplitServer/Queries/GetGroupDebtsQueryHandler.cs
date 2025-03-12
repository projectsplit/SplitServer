using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupDebtsQueryHandler : IRequestHandler<GetGroupDebtsQuery, Result<GetGroupDebtsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly DebtService _debtService;

    public GetGroupDebtsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        DebtService debtService)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _debtService = debtService;
    }

    public async Task<Result<GetGroupDebtsResponse>> Handle(GetGroupDebtsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDebtsResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupDebtsResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupDebtsResponse>("User must be a group member");
        }

        return new GetGroupDebtsResponse
        {
            Debts = await _debtService.GetDebts(query.GroupId, ct)
        };
    }

}