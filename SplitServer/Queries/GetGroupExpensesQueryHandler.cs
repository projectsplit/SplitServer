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

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        var expenses = await _expensesRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        return new GroupExpensesResponse
        {
            Expenses = expenses.Select(
                x => new ExpenseResponseItem
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
                    Labels = x.Labels.Select(id => groupLabels.GetValueOrDefault(id, Label.Empty)).ToList(),
                    Location = x.Location,
                }).ToList(),
            Next = GetNext(query, expenses)
        };
    }

    private static string? GetNext(GetGroupExpensesQuery query, List<GroupExpense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}