using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetGroupExpensesQueryHandler : IRequestHandler<GetGroupExpensesQuery, Result<GetGroupExpensesResponse>>
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

    public async Task<Result<GetGroupExpensesResponse>> Handle(GetGroupExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupExpensesResponse>($"User with id {query.UserId} was not found");
        }

        var groupMaybe = await _groupsRepository.GetById(query.GroupId, ct);

        if (groupMaybe.HasNoValue)
        {
            return Result.Failure<GetGroupExpensesResponse>($"Group with id {query.GroupId} was not found");
        }
        
        var group = groupMaybe.Value;

        if (group.Members.All(x => x.UserId != query.UserId))
        {
            return Result.Failure<GetGroupExpensesResponse>("User must be a group member");
        }

        var nextDetails = ParseNext(query.Next);

        var expenses = await _expensesRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Occured,
            nextDetails?.Created,
            ct);

        return new GetGroupExpensesResponse
        {
            Expenses = expenses,
            Next = CreateNext(expenses, query.PageSize)
        };
    }

    private static string? CreateNext(List<Expense> expenses, int pageSize)
    {
        if (expenses.Count < pageSize)
        {
            return default;
        }
        
        var lastExpenseInPage = expenses.Last();

        var newNextDetails = new NextExpensePageDetails
        {
            Created = lastExpenseInPage.Created,
            Occured = lastExpenseInPage.Occured
        };
        
        var jsonString = JsonSerializer.Serialize(newNextDetails);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
    }

    private static NextExpensePageDetails? ParseNext(string? next)
    {
        if (string.IsNullOrEmpty(next))
        {
            return default;
        }

        var jsonString = Encoding.UTF8.GetString(Convert.FromBase64String(next));
        
        return JsonSerializer.Deserialize<NextExpensePageDetails>(jsonString);
    }
}

internal class NextExpensePageDetails
{
    public required DateTime Created { get; init; }
    
    public required DateTime Occured { get; init; }
}