using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchGroupExpensesQueryHandler : IRequestHandler<SearchGroupExpensesQuery, Result<GroupExpensesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;

    public SearchGroupExpensesQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
    }

    public async Task<Result<GroupExpensesResponse>> Handle(SearchGroupExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GroupExpensesResponse>($"User with id {query.UserId} was not found");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GroupExpensesResponse>($"Group with id {query.GroupId} was not found");
        }

        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GroupExpensesResponse>("User must be a group member");
        }

        var groupLabels = group.Labels.ToDictionary(x => x.Id);

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        var minOccurred = query.After?.Date.ToUtc(userTimeZoneId);
        var maxOccurred = query.Before?.Date.AddDays(1).AddTicks(-1).ToUtc(userTimeZoneId);

        var expenses = await _expensesRepository.Search(
            query.GroupId,
            query.SearchTerm,
            minOccurred,
            maxOccurred,
            query.ParticipantIds,
            query.PayerIds,
            query.LabelIds,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        var emptyLabel = new Label { Id = "", Text = "", Color = "" };

        return new GroupExpensesResponse
        {
            Expenses = expenses.Select(x => new ExpenseResponseItem
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
                Payments = x.Payments,
                Shares = x.Shares,
                Labels = x.Labels.Select(id => groupLabels.GetValueOrDefault(id, emptyLabel)).ToList(),
                Location = x.Location,
            }).ToList(),
            Next = GetNext(query, expenses)
        };
    }

    private static string? GetNext(SearchGroupExpensesQuery query, List<Expense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}