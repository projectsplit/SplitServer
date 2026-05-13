using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchNonGroupExpenseUsersQuery : IRequest<Result<SearchNonGroupUsersResponse>>
{
    public required string UserId { get; init; }
}