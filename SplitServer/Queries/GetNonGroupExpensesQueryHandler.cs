using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetNonGroupExpensesQueryHandler : IRequestHandler<GetNonGroupExpensesQuery, Result<NonGroupExpensesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;

    public GetNonGroupExpensesQueryHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository,
        IUserLabelsRepository userLabelsRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
        _userLabelsRepository = userLabelsRepository;
    }

    public async Task<Result<NonGroupExpensesResponse>> Handle(GetNonGroupExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<NonGroupExpensesResponse>($"User with id {query.UserId} was not found");
        }

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        var nonGroupExpenses = await _expensesRepository.GetNonGroupByUserId(
            query.UserId,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

        var userLabels = await _userLabelsRepository.GetByUserId(query.UserId, ct);

        return new NonGroupExpensesResponse
        {
            Expenses = nonGroupExpenses.Select(x =>
                new NonGroupExpenseResponseItem
                {
                    Id = x.Id,
                    Created = x.Created,
                    Updated = x.Updated,
                    CreatorId = x.CreatorId,
                    Amount = x.Amount,
                    Occurred = x.Occurred,
                    Description = x.Description,
                    Currency = x.Currency,
                    Payments = x.Payments,
                    Shares = x.Shares,
                    Labels = x.Labels
                        .Select(text =>
                        {
                            var userLabel = userLabels.FirstOrDefault(l => l.Text == text);

                            return new Label
                            {
                                Id = text,
                                Text = userLabel?.Text ?? text,
                                Color = userLabel?.Color ?? ""
                            };
                        })
                        .ToList(),
                    Location = x.Location,
                }).ToList(),
            Next = GetNext(query, nonGroupExpenses)
        };
    }

    private static string? GetNext(GetNonGroupExpensesQuery query, List<NonGroupExpense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}