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

        var expenses = await _expensesRepository.SearchNonGroup(
            query.UserId,
            query.SearchTerm,
            query.After?.ToUtc(userTimeZoneId),
            query.Before?.ToUtc(userTimeZoneId),
            query.ParticipantIds,
            query.PayerIds,
            query.Labels,
            query.PageSize,
            nextDetails?.Occurred,
            nextDetails?.Created,
            ct);

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
                })
                .ToList(),
            Next = GetNext(query, expenses)
        };
    }

    private static string? GetNext(SearchNonGroupExpensesQuery query, List<NonGroupExpense> expenses)
    {
        return Next.Create(
            expenses,
            query.PageSize,
            x => new NextExpensePageDetails { Created = x.Last().Created, Occurred = x.Last().Occurred });
    }
}