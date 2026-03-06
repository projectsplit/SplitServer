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

        var labelIds = query.LabelIds?.Select(id => id.Contains('_') ? id.Split('_')[1] : id).ToArray();

        var expenses = await _expensesRepository.SearchPersonalByUserId(
            query.UserId,
            memberIds,
            query.SearchTerm,
            query.After?.ToUtc(userTimeZoneId),
            query.Before?.ToUtc(userTimeZoneId),
            labelIds,
            query.PageSize + 1,
            nextDetails?.Occurred,
            nextDetails?.Created,
            PaginationDirection.Older,
            false,
            ct);

        bool hasMoreOlder = false;
        if (expenses.Count > query.PageSize)
        {
            hasMoreOlder = true;
            expenses.RemoveAt(expenses.Count - 1);
        }

        bool hasMoreNewer = query.Next != null;
        
        var userLabels = await _userLabelsRepository.GetByUserId(query.UserId, ct);
        var groupIds = expenses.OfType<GroupExpense>().Select(ge => ge.GroupId).Distinct().ToList();
        var groups = await _groupsRepository.GetByIds(groupIds, ct);
        var groupLabels = groups.ToDictionary(g => g.Id, g => g.Labels.ToDictionary(l => l.Id));
        
        var responseItems = MapToResponseItems(query.UserId, memberIds, expenses,userLabels, groupLabels);

        return new PersonalExpensesResponse
        {
            Expenses = responseItems,
            Next = hasMoreOlder ? CreateToken(expenses.Last(), false) : null,
            Previous = hasMoreNewer ? CreateToken(expenses.First(), false) : null
        };
    }

    private static string? CreateToken(Expense expense, bool isJumpTo)
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
    
    private List<PersonalExpenseResponseItem> MapToResponseItems(
        string currentUserId,
        List<string> memberIds,
        List<Expense> expenses,
        List<UserLabel>? userLabels,
        Dictionary<string, Dictionary<string, Label>> groupLabels
       )
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
                Labels = GetLabels(e, currentUserId, userLabels, groupLabels)
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

    private static List<Label> GetLabels(
        Expense e, 
        string userId, 
        List<UserLabel>? userLabels,
        Dictionary<string, Dictionary<string, Label>> groupLabels)
    {
        if (e is GroupExpense ge)
        {
            var labelsDict = groupLabels.GetValueOrDefault(ge.GroupId);
            return ge.Labels.Select(id => labelsDict?.GetValueOrDefault(id) ?? new Label { Id = id, Text = id, Color = "" }).ToList();
        }

        var labelTexts = e switch
        {
            NonGroupExpense nge => nge.Labels,
            PersonalExpense pe => pe.Labels,
            _ => new List<string>()
        };

        return labelTexts.Select(text =>
        {
            var userLabel = userLabels?.FirstOrDefault(l => string.Equals(l.Text, text, StringComparison.OrdinalIgnoreCase));

            return new Label
            {
                Id = $"{userId}_{text}",
                Text = userLabel?.Text ?? text,
                Color = userLabel?.Color ?? ""
            };
        }).ToList();
    }

    private static string? GetNext(SearchPersonalExpensesQuery query, List<Expense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}
