using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetRecurringExpensesQueryHandler : IRequestHandler<GetRecurringExpensesQuery, Result<GetRecurringExpensesResponse>>
{
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IGroupsRepository _groupsRepository;

    public GetRecurringExpensesQueryHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        IExpensesRepository expensesRepository,
        IGroupsRepository groupsRepository)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _expensesRepository = expensesRepository;
        _groupsRepository = groupsRepository;
    }

    public async Task<Result<GetRecurringExpensesResponse>> Handle(GetRecurringExpensesQuery query, CancellationToken ct)
    {
        var templates = await _recurringExpensesRepository.GetAllByUserId(query.UserId, ct);

        if (templates.Count == 0)
        {
            return new GetRecurringExpensesResponse { RecurringExpenses = [] };
        }

        // Both lookups are batched: the list mixes scopes, so resolving group names and last
        // occurrences one row at a time would be a query per template.
        var lastExpenseIds = templates
            .Select(x => x.LastExpenseId)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct()
            .ToList();

        var lastExpenses = lastExpenseIds.Count > 0
            ? (await _expensesRepository.GetByIds(lastExpenseIds, ct)).ToDictionary(x => x.Id)
            : new Dictionary<string, Expense>();

        var groupIds = templates
            .OfType<GroupRecurringExpense>()
            .Select(x => x.GroupId)
            .Distinct()
            .ToList();

        var groupNames = groupIds.Count > 0
            ? (await _groupsRepository.GetByIds(groupIds, ct)).ToDictionary(x => x.Id, x => x.Name)
            : new Dictionary<string, string>();

        return new GetRecurringExpensesResponse
        {
            RecurringExpenses = templates.Select(x => ToResponseItem(x, lastExpenses, groupNames)).ToList()
        };
    }

    private static RecurringExpenseResponseItem ToResponseItem(
        RecurringExpense template,
        IReadOnlyDictionary<string, Expense> lastExpenses,
        IReadOnlyDictionary<string, string> groupNames)
    {
        // The expense may have been deleted by hand since it was generated; the row still lists,
        // it just has nothing to jump to.
        Expense? lastExpense = null;

        if (template.LastExpenseId is not null && lastExpenses.TryGetValue(template.LastExpenseId, out var found))
        {
            lastExpense = found;
        }

        var groupTemplate = template as GroupRecurringExpense;
        var nonGroupTemplate = template as NonGroupRecurringExpense;

        var groupId = groupTemplate?.GroupId;

        return new RecurringExpenseResponseItem
        {
            Id = template.Id,
            Created = template.Created,
            Updated = template.Updated,
            TransactionType = template switch
            {
                GroupRecurringExpense => ExpenseResponseType.Group,
                NonGroupRecurringExpense => ExpenseResponseType.NonGroup,
                _ => ExpenseResponseType.Personal
            },
            GroupId = groupId,
            GroupName = groupId is not null && groupNames.TryGetValue(groupId, out var name) ? name : null,
            Amount = template.Amount,
            Currency = template.Currency,
            Description = template.Description,
            Location = template.Location,
            Labels = template.Labels,
            Schedule = template.Schedule,
            AnchorDate = template.AnchorDate,
            NextOccurrence = template.NextOccurrence,
            IsPaused = template.IsPaused,
            LastError = template.LastError,
            Payments = groupTemplate?.Payments,
            Shares = groupTemplate?.Shares,
            NonGroupPayments = nonGroupTemplate?.Payments,
            NonGroupShares = nonGroupTemplate?.Shares,
            LastExpenseId = lastExpense?.Id,
            LastExpenseOccurred = lastExpense?.Occurred,
            LastExpenseCreated = lastExpense?.Created
        };
    }
}
