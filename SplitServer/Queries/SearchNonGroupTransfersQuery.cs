using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class SearchNonGroupTransfersQuery : IRequest<Result<NonGroupTransfersResponse>>
{
    public required string UserId { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }
    public required DateTime? Before { get; init; }
    public required DateTime? After { get; init; }
    public required string? SearchTerm { get; init; }
    public required string[]? ReceiverIds { get; init; }
    public required string[]? SenderIds { get; init; }
}