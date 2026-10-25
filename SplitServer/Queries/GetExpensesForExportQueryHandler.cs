using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;
using SplitServer.Services.CurrencyExchangeRate;

namespace SplitServer.Queries;

public class GetExpensesForExportQueryHandler : IRequestHandler<GetExpensesForExportQuery, Result<GetExpensesForExportResponse>>
{
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;
    private readonly CurrencyExchangeRateService _currencyExchangeRateService;
    private readonly ValidationService _validationService;

    public GetExpensesForExportQueryHandler(
        IUserPreferencesRepository userPreferencesRepository,
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        IUserLabelsRepository userLabelsRepository,
        CurrencyExchangeRateService currencyExchangeRateService,
        ValidationService validationService)
    {
        _userPreferencesRepository = userPreferencesRepository;
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _userLabelsRepository = userLabelsRepository;
        _currencyExchangeRateService = currencyExchangeRateService;
        _validationService = validationService;
    }

    public async Task<Result<GetExpensesForExportResponse>> Handle(GetExpensesForExportQuery query, CancellationToken ct)
    {
        if (query.Scope == BudgetScope.None)
        {
            return Result.Failure<GetExpensesForExportResponse>("Choose at least one kind of expense to export");
        }

        if (query.EndDate.Date < query.StartDate.Date)
        {
            return Result.Failure<GetExpensesForExportResponse>("The end date is before the start date");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var timeZoneId = userPreferencesMaybe.GetValueOrDefault()?.TimeZone ?? DefaultValues.TimeZone;
        var currency = userPreferencesMaybe.GetValueOrDefault()?.Currency ?? DefaultValues.Currency;

        // The window is whole days where the user lives, converted once, the way the budget cycle
        // and the analytics charts do it. Comparing the stored UTC instants against the local dates
        // directly would shift the window by their offset.
        var utcStart = query.StartDate.ToUtc(timeZoneId);
        var utcEnd = query.EndDate.EndOfDay().ToUtc(timeZoneId);

        // Current membership only, so a group the user left contributes nothing — the same rule
        // every other total in the app follows.
        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var groupsById = groups.ToDictionary(g => g.Id);

        var expenses = new List<Expense>();

        if (query.Scope.HasFlag(BudgetScope.Group))
        {
            var selectedGroups = query.TargetGroupIds.Count == 0
                ? groups
                : groups.Where(g => query.TargetGroupIds.Contains(g.Id)).ToList();

            var myMemberIds = selectedGroups
                .Select(g => g.Members.First(m => m.UserId == query.UserId).Id)
                .ToList();

            if (myMemberIds.Count > 0)
            {
                expenses.AddRange(await _expensesRepository.GetGroupExpensesByMemberIds(myMemberIds, utcStart, utcEnd, ct));
            }
        }

        if (query.Scope.HasFlag(BudgetScope.NonGroup))
        {
            expenses.AddRange(await _expensesRepository.GetNonGroupExpensesByUserId(query.UserId, utcStart, utcEnd, ct));
        }

        if (query.Scope.HasFlag(BudgetScope.Personal))
        {
            // That query answers the "your expenses" screen, which lists every kind the user is in.
            // Here personal means the ones that are theirs alone; the other kinds have their own flag.
            var ownExpenses = await _expensesRepository.GetPersonalExpensesByUserId(query.UserId, [], ct, utcStart, utcEnd);
            expenses.AddRange(ownExpenses.OfType<PersonalExpense>());
        }

        expenses = expenses
            .OrderBy(x => x.Occurred)
            .ThenBy(x => x.Created)
            .ToList();

        var names = await LoadNames(query.UserId, expenses, groupsById, ct);
        var userLabels = await _userLabelsRepository.GetByUserId(query.UserId, ct);

        // Rates are optional. A report with the original amounts and no converted column is still
        // a report; refusing to produce one because a quote is missing would not be.
        var ratesResult = await _currencyExchangeRateService.GetRatesForDates(
            expenses.Select(x => DateOnly.FromDateTime(x.Occurred)).Distinct().ToList(),
            ct);

        var rates = ratesResult.IsSuccess ? ratesResult.Value : null;

        var currencyResult = _validationService.ValidateCurrency(currency);
        var decimalDigits = currencyResult.IsSuccess ? currencyResult.Value.SignificantDecimalDigits : 2;

        var items = expenses
            .Select(expense =>
            {
                var (yourShare, youPaid, paidBy, splitWith) = Participation(expense, query.UserId, groupsById, names);

                return new ExportExpenseItem
                {
                    Id = expense.Id,
                    Occurred = expense.Occurred,
                    Description = expense.Description,
                    Type = expense switch
                    {
                        PersonalExpense => ExpenseResponseType.Personal,
                        GroupExpense => ExpenseResponseType.Group,
                        NonGroupExpense => ExpenseResponseType.NonGroup,
                        _ => throw new ArgumentOutOfRangeException(nameof(expense))
                    },
                    GroupId = (expense as GroupExpense)?.GroupId,
                    GroupName = expense is GroupExpense ge ? groupsById.GetValueOrDefault(ge.GroupId)?.Name : null,
                    Currency = expense.Currency,
                    Amount = expense.Amount,
                    YourShare = yourShare,
                    YouPaid = youPaid,
                    YourShareConverted = Convert(rates, yourShare, expense, currency, decimalDigits),
                    YouPaidConverted = Convert(rates, youPaid, expense, currency, decimalDigits),
                    PaidBy = paidBy,
                    SplitWith = splitWith,
                    Labels = LabelTexts(expense, groupsById, userLabels),
                    Place = expense.Location?.Google?.Name ?? expense.Location?.Google?.Address,
                    IsRecurring = expense.RecurringExpenseId is not null
                };
            })
            .ToList();

        return new GetExpensesForExportResponse
        {
            Currency = currency,
            TimeZone = timeZoneId,
            Expenses = items
        };
    }

    private static decimal? Convert(PeriodExchangeRates? rates, decimal amount, Expense expense, string currency, int decimalDigits)
    {
        if (rates is null)
        {
            return null;
        }

        return Math.Round(rates.Convert(amount, expense.Currency, currency, DateOnly.FromDateTime(expense.Occurred)), decimalDigits);
    }

    /// <summary>
    /// Who is who, for every expense in the report. Group members are named by their account, guests
    /// by the name the group gave them, and both are keyed by member id since that is what an expense
    /// holds. Non-group expenses hold user ids directly.
    /// </summary>
    private async Task<Names> LoadNames(
        string userId,
        List<Expense> expenses,
        Dictionary<string, Group> groupsById,
        CancellationToken ct)
    {
        var groupsInReport = expenses
            .OfType<GroupExpense>()
            .Select(x => x.GroupId)
            .Distinct()
            .Select(id => groupsById.GetValueOrDefault(id))
            .OfType<Group>()
            .ToList();

        // The user themselves is always in the list: a personal expense names them as its payer.
        var userIds = groupsInReport
            .SelectMany(g => g.Members.Select(m => m.UserId))
            .Concat(expenses.OfType<NonGroupExpense>().SelectMany(x => x.Payments.Select(p => p.UserId).Concat(x.Shares.Select(s => s.UserId))))
            .Append(userId)
            .Distinct()
            .ToList();

        var users = await _usersRepository.GetByIds(userIds, ct);
        var usersById = users.ToDictionary(u => u.Id);

        string UserName(string id) => usersById.GetValueOrDefault(id)?.Username ?? DeletedUser.Username(id);

        var byMemberId = new Dictionary<string, ExportParticipant>();

        foreach (var group in groupsInReport)
        {
            foreach (var member in group.Members)
            {
                byMemberId[member.Id] = new ExportParticipant { Name = UserName(member.UserId), IsYou = member.UserId == userId, Amount = 0 };
            }

            foreach (var guest in group.Guests)
            {
                byMemberId[guest.Id] = new ExportParticipant { Name = guest.Name, IsYou = false, Amount = 0 };
            }
        }

        var byUserId = userIds.ToDictionary(
            id => id,
            id => new ExportParticipant { Name = UserName(id), IsYou = id == userId, Amount = 0 });

        return new Names(byMemberId, byUserId);
    }

    private static (decimal YourShare, decimal YouPaid, List<ExportParticipant> PaidBy, List<ExportParticipant> SplitWith) Participation(
        Expense expense,
        string userId,
        Dictionary<string, Group> groupsById,
        Names names)
    {
        switch (expense)
        {
            case PersonalExpense personal:
            {
                var you = names.User(userId) with { Amount = personal.Amount };
                return (personal.Amount, personal.Amount, [you], []);
            }

            case GroupExpense group:
            {
                var paidBy = group.Payments.Select(p => names.Member(p.MemberId) with { Amount = p.Amount }).ToList();
                var splitWith = group.Shares.Select(s => names.Member(s.MemberId) with { Amount = s.Amount }).ToList();

                // Which member is the user is a property of the group, not of the expense: a member
                // id a departed person handed to a guest is nobody's now.
                var myMemberId = groupsById.GetValueOrDefault(group.GroupId)?.Members.FirstOrDefault(m => m.UserId == userId)?.Id;

                return (
                    group.Shares.FirstOrDefault(s => s.MemberId == myMemberId)?.Amount ?? 0,
                    group.Payments.FirstOrDefault(p => p.MemberId == myMemberId)?.Amount ?? 0,
                    paidBy,
                    splitWith);
            }

            case NonGroupExpense nonGroup:
            {
                var paidBy = nonGroup.Payments.Select(p => names.User(p.UserId) with { Amount = p.Amount }).ToList();
                var splitWith = nonGroup.Shares.Select(s => names.User(s.UserId) with { Amount = s.Amount }).ToList();

                return (
                    nonGroup.Shares.FirstOrDefault(s => s.UserId == userId)?.Amount ?? 0,
                    nonGroup.Payments.FirstOrDefault(p => p.UserId == userId)?.Amount ?? 0,
                    paidBy,
                    splitWith);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(expense));
        }
    }

    private static List<string> LabelTexts(Expense expense, Dictionary<string, Group> groupsById, List<UserLabel> userLabels)
    {
        if (expense is GroupExpense groupExpense)
        {
            var groupLabels = groupsById.GetValueOrDefault(groupExpense.GroupId)?.Labels ?? [];

            return groupExpense.Labels
                .Select(id => groupLabels.FirstOrDefault(l => l.Id == id)?.Text ?? id)
                .ToList();
        }

        // Personal and non-group expenses store the label text itself; the user's own list only
        // lends it the casing they chose.
        return expense.Labels
            .Select(text => userLabels.FirstOrDefault(l => string.Equals(l.Text, text, StringComparison.OrdinalIgnoreCase))?.Text ?? text)
            .ToList();
    }

    private sealed record Names(
        Dictionary<string, ExportParticipant> ByMemberId,
        Dictionary<string, ExportParticipant> ByUserId)
    {
        private static readonly ExportParticipant Unknown = new() { Name = "Unknown", IsYou = false, Amount = 0 };

        public ExportParticipant Member(string memberId) => ByMemberId.GetValueOrDefault(memberId) ?? Unknown;

        public ExportParticipant User(string userId) => ByUserId.GetValueOrDefault(userId) ?? Unknown;
    }
}
