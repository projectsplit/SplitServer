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

        List<NonGroupExpense> nonGroupExpenses;
        bool hasMoreNewer = false;
        bool hasMoreOlder = false;

        if (nextDetails?.IsJumpTo == true)
        {
            var newerTargetCount = query.PageSize / 2;
            var newerItems = await _expensesRepository.GetNonGroupByUserId(
                query.UserId,
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
            var olderItems = await _expensesRepository.GetNonGroupByUserId(
                query.UserId,
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

            nonGroupExpenses = newerItems.Concat(olderItems).ToList();
        }
        else
        {
            nonGroupExpenses = await _expensesRepository.GetNonGroupByUserId(
                query.UserId,
                query.PageSize + 1,
                nextDetails?.Occurred,
                nextDetails?.Created,
                PaginationDirection.Older,
                false,
                ct);

            if (nonGroupExpenses.Count > query.PageSize)
            {
                hasMoreOlder = true;
                nonGroupExpenses.RemoveAt(nonGroupExpenses.Count - 1);
            }

            hasMoreNewer = query.Next != null;
        }

        var uniqueUserIds = nonGroupExpenses
            .SelectMany(e => e.Payments.Select(p => p.UserId).Concat(e.Shares.Select(s => s.UserId)))
            .Distinct()
            .ToList();

        var users = await _usersRepository.GetByIds(uniqueUserIds, ct);
        var usersById = users.ToDictionary(u => u.Id);

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
                    TransactionType = ExpenseType.NonGroup,
                    Payments = x.Payments
                        .Select(p => new GetNonGroupPaymentItem
                        {
                            Amount = p.Amount,
                            UserId = p.UserId,
                            Username = usersById.GetValueOrDefault(p.UserId)?.Username ?? DeletedUser.Username(p.UserId)
                        })
                        .ToList(),
                    Shares = x.Shares
                        .Select(s => new GetNonGroupShareItem
                        {
                            Amount = s.Amount,
                            UserId = s.UserId,
                            Username = usersById.GetValueOrDefault(s.UserId)?.Username ?? DeletedUser.Username(s.UserId)
                        })
                        .ToList(),
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
            Next = hasMoreOlder ? CreateToken(nonGroupExpenses.Last(), false) : null,
            Previous = hasMoreNewer ? CreateToken(nonGroupExpenses.First(), false) : null
        };
    }

    private static string? CreateToken(NonGroupExpense expense, bool isJumpTo)
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

    private static string? GetNext(GetNonGroupExpensesQuery query, List<NonGroupExpense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}