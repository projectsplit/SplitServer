using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchPersonalExpensesQueryHandler : IRequestHandler<SearchPersonalExpensesQuery, Result<PersonalExpensesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;

    public SearchPersonalExpensesQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IUserLabelsRepository userLabelsRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _userLabelsRepository = userLabelsRepository;
    }

    public async Task<Result<PersonalExpensesResponse>> Handle(SearchPersonalExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<PersonalExpensesResponse>($"User with id {query.UserId} was not found");
        }

        var user = userMaybe.Value;
        
        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var userGroups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var memberIds = userGroups.SelectMany(g => g.Members.Where(m => m.UserId == query.UserId).Select(m => m.Id)).ToList();

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        var expenses = await _expensesRepository.SearchPersonalByUserId(
            query.UserId,
            memberIds,
            query.SearchTerm,
            query.After?.ToUtc(userTimeZoneId),
            query.Before?.ToUtc(userTimeZoneId),
            query.Labels,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        var responseItems = MapToResponseItems(query.UserId, memberIds, expenses, userGroups);
        var userLabels = await _userLabelsRepository.GetByUserId(query.UserId, ct);
        var labelsByText = userLabels.ToDictionary(l => l.Text);

        foreach (var item in responseItems)
        {
            var enrichedLabels = new List<Label>();
            foreach (var l in item.Labels)
            {
                enrichedLabels.Add(labelsByText.TryGetValue(l.Text, out var ul) ? l with { Color = ul.Color } : l);
            }
            item.Labels.Clear();
            item.Labels.AddRange(enrichedLabels);
        }

        return new PersonalExpensesResponse
        {
            Expenses = responseItems,
            Next = GetNext(query, expenses)
        };
    }

    // This method is identical to the one in GetPersonalExpensesQueryHandler
    // In a real project, this should be refactored into a shared service or mapper
    private List<PersonalExpenseResponseItem> MapToResponseItems(
        string currentUserId,
        List<string> memberIds,
        List<Expense> expenses,
        IList<Group> userGroups)
    {
        var result = new List<PersonalExpenseResponseItem>();

        foreach (var e in expenses)
        {
            var item = new PersonalExpenseResponseItem
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                CreatorId = e.CreatorId,
                Amount = GetUserShareAmount(e, currentUserId, memberIds),
                Occurred = e.Occurred,
                Description = e.Description,
                Currency = e.Currency,
                Location = e.Location,
                TransactionType = e switch
                {
                    PersonalExpense => ExpenseType.Personal,
                    GroupExpense => ExpenseType.Group,
                    NonGroupExpense => ExpenseType.NonGroup,
                    _ => throw new ArgumentOutOfRangeException(nameof(e))
                },
                GroupId = (e as GroupExpense)?.GroupId,
                Labels = GetLabels(e)
            };

            result.Add(item);
        }

        return result;
    }

    private decimal GetUserShareAmount(Expense e, string userId, List<string> memberIds)
    {
        return e switch
        {
            PersonalExpense pe => pe.Amount,
            NonGroupExpense nge => nge.Shares.FirstOrDefault(s => s.UserId == userId)?.Amount ?? 0,
            GroupExpense ge => ge.Shares.FirstOrDefault(s => memberIds.Contains(s.MemberId))?.Amount ?? 0,
            _ => 0
        };
    }

    private List<Label> GetLabels(Expense e)
    {
        var labelTexts = e switch
        {
            GroupExpense ge => ge.Labels,
            NonGroupExpense nge => nge.Labels,
            PersonalExpense pe => pe.Labels,
            _ => new List<string>()
        };

        return labelTexts.Select(t => new Label { Id = t, Text = t, Color = "" }).ToList();
    }

    private static string? GetNext(SearchPersonalExpensesQuery query, List<Expense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}
