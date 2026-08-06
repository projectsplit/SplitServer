using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetRecurringExpensesQuery : IRequest<Result<GetRecurringExpensesResponse>>
{
    public required string UserId { get; init; }
}
