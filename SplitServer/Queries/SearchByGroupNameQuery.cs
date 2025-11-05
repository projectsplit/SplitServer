using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchByGroupNameQuery : IRequest<Result<GetGroupsResponse>>
{
    public required string UserId { get; init; }
    public required int PageSize { get; init; }
    public required string? Keyword { get; init; }
    public required string? Next { get; init; }
}
