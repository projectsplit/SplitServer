using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
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

        var responseItems = new List<GetInactiveBudgetsInfoResponseItem>();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;

        foreach (var budget in budgets.OrderByDescending(b => b.Created))
        {
            var isExpired = false;
            var datesResult = _budgetService.CalculateDates(budget, timeZoneId);

            if (datesResult.IsFailure)
            {
                continue;
            }

            var (startDate, endDate) = datesResult.Value;

            if (budget.IsActive && budget.Frequency == BudgetFrequency.Custom && endDate < now)
            {
                var updatedBudget = budget with { IsActive = false };
                await _budgetsRepository.Update(updatedBudget, ct);
                isExpired = true;
            }

            if (!budget.IsActive || isExpired)
            {
                responseItems.Add(new GetInactiveBudgetsInfoResponseItem
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
                });
            }
        }

        return new GetInactiveBudgetsInfoResponse
        {
            Budgets = responseItems
        };
    }
}