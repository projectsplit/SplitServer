using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Requests;

namespace SplitServer.Commands;

public class EditExpenseCommand : IRequest<Result>
{
    public required string ExpenseId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required DateTime? Occurred { get; init; }
    public required List<GroupPayment> Payments { get; init; }
    public required List<GroupShare> Shares { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    public required Location? Location { get; init; }
}