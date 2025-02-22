using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Repositories;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Queries;

public class GetLabelsQueryHandler : IRequestHandler<GetLabelsQuery, Result<GetLabelsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;

    public GetLabelsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result<GetLabelsResponse>> Handle(GetLabelsQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetLabelsResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetLabelsResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetLabelsResponse>("User must be a group member");
        }

        var allLabels = await _expensesRepository.GetAllLabels(query.GroupId, ct);

        return new GetLabelsResponse
        {
            Labels = AutoCompleteSearch(allLabels, query.Query)
                .Select(x => x.Label)
                .Take(query.Limit)
                .ToList()
        };
    }

    private static IEnumerable<LabelCount> AutoCompleteSearch(List<LabelCount> labelCounts, string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return labelCounts;
        }

        return labelCounts
            .Where(x => x.Label.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(x => x.Label.Length)
            .ThenBy(x => x.Count)
            .ThenBy(x => x.Label);
    }
}