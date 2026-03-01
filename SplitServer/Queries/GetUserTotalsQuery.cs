using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUserTotalsQuery: IRequest<Result<GetUserTotalsResponse>>
{
    public required string UserId { get; init; }
    public string? SearchTerm { get; init; }
    public DateTime? After { get; init; }
    public DateTime? Before { get; init; }
    public string[]? LabelIds { get; init; }

}
