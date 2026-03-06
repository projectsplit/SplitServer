using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class SearchNonGroupExpensesQueryHandler : IRequestHandler<SearchNonGroupExpensesQuery, Result<NonGroupExpensesResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;

    public SearchNonGroupExpensesQueryHandler(
        IUsersRepository usersRepository,
        IExpensesRepository expensesRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IUserLabelsRepository userLabelsRepository)
    {
        _usersRepository = usersRepository;
        _expensesRepository = expensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _userLabelsRepository = userLabelsRepository;
    }

    public async Task<Result<NonGroupExpensesResponse>> Handle(SearchNonGroupExpensesQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<NonGroupExpensesResponse>($"User with id {query.UserId} was not found");
        }

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var userTimeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var nextDetails = Next.Parse<NextExpensePageDetails>(query.Next);

        List<NonGroupExpense> expenses;
        bool hasMoreNewer = false;
        bool hasMoreOlder = false;

        if (nextDetails?.IsJumpTo == true)
        {
            var newerTargetCount = query.PageSize / 2;
            var newerItems = await _expensesRepository.SearchNonGroup(
                query.UserId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
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
            var olderItems = await _expensesRepository.SearchNonGroup(
                query.UserId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
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

            expenses = newerItems.Concat(olderItems).ToList();
        }
        else
        {
            expenses = await _expensesRepository.SearchNonGroup(
                query.UserId,
                query.SearchTerm,
                query.After?.ToUtc(userTimeZoneId),
                query.Before?.ToUtc(userTimeZoneId),
                query.ParticipantIds,
                query.PayerIds,
                query.LabelIds,
                query.PageSize + 1,
                nextDetails?.Occurred,
                nextDetails?.Created,
                PaginationDirection.Older,
                false,
                ct);

            if (expenses.Count > query.PageSize)
            {
                hasMoreOlder = true;
                expenses.RemoveAt(expenses.Count - 1);
            }

            hasMoreNewer = query.Next != null;
        }

        var uniqueUserIds = expenses
            .SelectMany(e => e.Payments.Select(p => p.UserId).Concat(e.Shares.Select(s => s.UserId)))
            .Distinct()
            .ToList();
        var users = await _usersRepository.GetByIds(uniqueUserIds.ToList(), ct);
        var usersById = users.ToDictionary(u => u.Id);

        var userLabels = await _userLabelsRepository.GetByUserId(query.UserId, ct);

        return new NonGroupExpensesResponse
        {
            Expenses = expenses
                .Select(x => new NonGroupExpenseResponseItem
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
                })
                .ToList(),
            Next = hasMoreOlder ? CreateToken(expenses.Last(), false) : null,
            Previous = hasMoreNewer ? CreateToken(expenses.First(), false) : null
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

    private static string? GetNext(SearchNonGroupExpensesQuery query, List<NonGroupExpense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}