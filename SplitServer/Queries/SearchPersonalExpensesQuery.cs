using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchPersonalExpensesQuery : IRequest<Result<PersonalExpensesResponse>>
{
    public required string UserId { get; init; }
    public DateTime? Before { get; init; }
    public DateTime? After { get; init; }
    public string? SearchTerm { get; init; }
    public string[]? Labels { get; init; }
    public required int PageSize { get; init; }
    public string? Next { get; init; }
}
