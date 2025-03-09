using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupJoinCodesQuery : IRequest<Result<GetGroupJoinCodesResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }
}