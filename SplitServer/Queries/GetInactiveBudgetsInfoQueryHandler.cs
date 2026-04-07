using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetInactiveBudgetsInfoQueryHandler : IRequestHandler<GetInactiveBudgetsInfoQuery, Result<GetInactiveBudgetsInfoResponse>>
{
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly BudgetService _budgetService;

    public GetInactiveBudgetsInfoQueryHandler(
        IBudgetsRepository budgetsRepository,
        IUserPreferencesRepository userPreferencesRepository,
        BudgetService budgetService)
    {
        _budgetsRepository = budgetsRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _budgetService = budgetService;
    }

    public async Task<Result<GetInactiveBudgetsInfoResponse>> Handle(GetInactiveBudgetsInfoQuery query, CancellationToken ct)
    {
        var budgets = await _budgetsRepository.GetAllByUserId(query.UserId, ct);

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);
        var timeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var responseItems = budgets
            .Where(b => !b.IsActive)
            .OrderByDescending(b => b.Created)
            .Select(budget =>
            {
                var datesResult = _budgetService.CalculateDates(budget, timeZoneId);

                if (datesResult.IsFailure)
                {
                    return null;
                }

                var (startDate, endDate) = datesResult.Value;

                return new GetInactiveBudgetsInfoResponseItem
                {
                    Id = budget.Id,
                    Amount = budget.Amount,
                    Description = budget.Description,
                    Currency = budget.Currency,
                    Frequency = budget.Frequency,
                    Scope = budget.Scope,
                    TargetGroupIds = budget.TargetGroupIds,
                    StartDate = startDate,
                    EndDate = endDate
                };
            })
            .WhereNotNull()
            .ToList();

        return new GetInactiveBudgetsInfoResponse
        {
            Budgets = responseItems
        };
    }
}