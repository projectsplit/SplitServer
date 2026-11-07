using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetNonGroupTransfersQuery : IRequest<Result<NonGroupTransfersResponse>>
{
    public required string UserId { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }

    /// <summary>The top of the list the app already has, asking for the items just newer than it.</summary>
    public string? Previous { get; init; }
}