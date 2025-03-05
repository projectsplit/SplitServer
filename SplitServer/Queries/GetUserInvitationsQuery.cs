using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUserInvitationsQuery : IRequest<Result<GetUserInvitationsResponse>>
{
    public required string UserId { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }
}