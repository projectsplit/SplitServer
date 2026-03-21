using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetInactiveBudgetsInfoQueryHandler : IRequestHandler<GetInactiveBudgetsInfoQuery, Result<GetInactiveBudgetsInfoResponse>>
{
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly BudgetService _budgetService;

    public GetInactiveBudgetsInfoQueryHandler(
        IBudgetsRepository budgetsRepository,
        BudgetService budgetService)
    {
        _budgetsRepository = budgetsRepository;
        _budgetService = budgetService;
    }

    public async Task<Result<GetInactiveBudgetsInfoResponse>> Handle(GetInactiveBudgetsInfoQuery query, CancellationToken ct)
    {
        var budgets = await _budgetsRepository.GetAllByUserId(query.UserId, ct);
        
        var inactiveBudgets = budgets.Where(b => !b.IsActive).ToList();
        
        var responseItems = new List<GetInactiveBudgetsInfoResponseItem>();
        
        foreach (var budget in inactiveBudgets)
        {
            var datesResult = _budgetService.CalculateDates(budget);
            if (datesResult.IsFailure)
            {
                continue;
            }

            var (startDate, endDate) = datesResult.Value;
            
            responseItems.Add(new GetInactiveBudgetsInfoResponseItem
            {
                Id = budget.Id,
                Amount = budget.Amount,
                Description = budget.Description,
                Currency = budget.Currency,
                Frequency = budget.Frequency,
                StartDate = startDate,
                EndDate = endDate
            });
        }

        return new GetInactiveBudgetsInfoResponse
        {
            Budgets = responseItems
        };
    }
}