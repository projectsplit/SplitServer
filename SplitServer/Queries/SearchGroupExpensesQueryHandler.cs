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

        List<GroupExpense> expenses;
        var hasMoreNewer = false;
        var hasMoreOlder = false;

        if (nextDetails?.IsJumpTo == true)
        {
            var newerTargetCount = query.PageSize / 2;
            var newerItems = await _expensesRepository.Search(
                query.GroupId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
                newerTargetCount + 1,
                nextDetails.Occurred,
                nextDetails.Created,
                PaginationDirection.Newer,
                true,
                ct);

            if (newerItems.Count > newerTargetCount)
            {
                hasMoreNewer = true;
                newerItems.RemoveAt(0);
            }

            var olderNeeded = query.PageSize - newerItems.Count;
            var olderItems = await _expensesRepository.Search(
                query.GroupId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
                olderNeeded + 1,
                nextDetails.Occurred,
                nextDetails.Created,
                PaginationDirection.Older,
                false,
                ct);

            if (olderItems.Count > olderNeeded)
            {
                hasMoreOlder = true;
                olderItems.RemoveAt(olderItems.Count - 1);
            }

            expenses = newerItems.Concat(olderItems).ToList();
        }
        else
        {
            expenses = await _expensesRepository.Search(
                query.GroupId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
                query.PageSize + 1,
                nextDetails?.Occurred,
                nextDetails?.Created,
                PaginationDirection.Older,
                false,
                ct);

            if (expenses.Count > query.PageSize)
            {
                hasMoreOlder = true;
                expenses.RemoveAt(expenses.Count - 1);
            }

            hasMoreNewer = query.Next != null;
        }

        return new GroupExpensesResponse
        {
            Expenses = expenses
                .Select(x => new GroupExpenseResponseItem
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
                    TransactionType = ExpenseResponseType.Group,
                    Payments = x.Payments,
                    Shares = x.Shares,
                    Labels = x.Labels.Select(id => groupLabels.GetValueOrDefault(id, Label.Empty)).ToList(),
                    Location = x.Location,
                })
                .ToList(),
            Next = hasMoreOlder ? CreateToken(expenses.Last(), false) : null,
            Previous = hasMoreNewer ? CreateToken(expenses.First(), false) : null
        };
    }

    private static string? CreateToken(GroupExpense expense, bool isJumpTo)
    {
        var details = new NextExpensePageDetails
        {
            Created = expense.Created,
            Occurred = expense.Occurred,
            IsJumpTo = isJumpTo
        };

        var jsonString = System.Text.Json.JsonSerializer.Serialize(details);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonString));
    }
}