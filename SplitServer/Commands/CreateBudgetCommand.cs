using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class CreateBudgetCommand : IRequest<Result<CreateBudgetResponse>>
{
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required string Currency { get; init; }
    public required BudgetFrequency Frequency { get; init; }
    public required BudgetScope Scope { get; init; }
    public bool? Activate { get; init; }
    public List<string>? TargetGroupIds { get; init; }
    public string? CommencementDay { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}