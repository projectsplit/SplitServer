using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetGroupExpensesQueryHandler : IRequestHandler<GetGroupExpensesQuery, Result<GroupExpensesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;

    public GetGroupExpensesQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result<GroupExpensesResponse>> Handle(GetGroupExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GroupExpensesResponse>($"User with id {query.UserId} was not found");
        }

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

        var page = await TransactionPages.Load<GroupExpense>(
            query.PageSize,
            Next.Parse<NextExpensePageDetails>(query.Next),
            Next.Parse<NextExpensePageDetails>(query.Previous),
            (limit, occurred, created, direction, inclusive) =>
                _expensesRepository.GetByGroupId(query.GroupId, limit, occurred, created, direction, inclusive, ct));

        var expenses = page.Items;

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
                    RecurringExpenseId = x.RecurringExpenseId,
                })
                .ToList(),
            Next = page.HasMoreOlder ? CreateToken(expenses.Last(), false) : null,
            Previous = page.HasMoreNewer ? CreateToken(expenses.First(), false) : null
        };
    }

    private static string CreateToken(GroupExpense expense, bool isJumpTo)
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