﻿using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

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

        var groupLabels = group.Labels.ToDictionary(x => x.Id);

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        var expenses = await _expensesRepository.GetByGroupId(
            query.GroupId,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        var emptyLabel = new Label { Id = "", Text = "", Color = "" };

        return new GetGroupExpensesResponse
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
                    Labels = x.Labels.Select(id => groupLabels.GetValueOrDefault(id, emptyLabel)).ToList(),
                    Location = x.Location,
                }).ToList(),
            Next = GetNext(query, expenses)
        };
    }

    private static string? GetNext(GetGroupExpensesQuery query, List<Expense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}

internal class NextExpensePageDetails
{
    public required DateTime Created { get; init; }

    public required DateTime Occurred { get; init; }
}