using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchGroupExpensesQuery : IRequest<Result<GroupExpensesResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required DateTime? Before { get; init; }
    public required DateTime? After { get; init; }
    public required string? SearchTerm { get; init; }
    public required string[]? LabelIds { get; init; }
    public required string[]? ParticipantIds { get; init; }
    public required string[]? PayerIds { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }
}