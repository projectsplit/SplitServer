using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Requests;

namespace SplitServer.Commands;

public class EditRecurringExpenseCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string RecurringExpenseId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }
    public required RecurrenceSchedule Schedule { get; init; }
    public required List<GroupPayment>? Payments { get; init; }
    public required List<GroupShare>? Shares { get; init; }
    public required List<Payment>? NonGroupPayments { get; init; }
    public required List<Share>? NonGroupShares { get; init; }
    public required List<LabelRequestItem> Labels { get; init; }
    public required Location? Location { get; init; }
}
