using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetActiveBudgetInfoQueryHandler : IRequestHandler<GetActiveBudgetInfoQuery, Result<GetActiveBudgetInfoResponse>>
{
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly BudgetService _budgetService;

    public GetActiveBudgetInfoQueryHandler(
        IBudgetsRepository budgetsRepository,
        BudgetService budgetService)
    {
        _budgetsRepository = budgetsRepository;
        _budgetService = budgetService;
    }

    public async Task<Result<GetActiveBudgetInfoResponse>> Handle(GetActiveBudgetInfoQuery query, CancellationToken ct)
    {
        var budgets = await _budgetsRepository.GetAllByUserId(query.UserId, ct);

        var activeBudget = budgets.FirstOrDefault(b => b.IsActive);

        if (activeBudget == null)
        {
            return Result.Failure<GetActiveBudgetInfoResponse>("No active budget found");
        }

        var spentAmountResult = await _budgetService.GetSpentAmount(activeBudget, ct);

        if (spentAmountResult.IsFailure)
        {
            return Result.Failure<GetActiveBudgetInfoResponse>(spentAmountResult.Error);
        }

        var datesResult = _budgetService.CalculateDates(activeBudget);
        if (datesResult.IsFailure)
        {
            return Result.Failure<GetActiveBudgetInfoResponse>(datesResult.Error);
        }

        var (startDate, endDate) = datesResult.Value;
        var now = DateTime.UtcNow.Date;
        var spentAmount = spentAmountResult.Value;

        var remainingDays = (endDate - now).Days;
        var daysPassed = (now - startDate).Days + 1;
        var averageSpentPerDay = spentAmount / daysPassed;

        return new GetActiveBudgetInfoResponse
        {
            TotalAmountSpent = spentAmount.ToString("F2"),
            Id = activeBudget.Id,
            Description = activeBudget.Description,
            RemainingDays = remainingDays.ToString(),
            AverageSpentPerDay = averageSpentPerDay.ToString("F2"),
            Goal = activeBudget.Amount.ToString("F2"),
            Currency = activeBudget.Currency,
            Frequency = activeBudget.Frequency,
            StartDate = startDate,
            EndDate = endDate
        };
    }
}