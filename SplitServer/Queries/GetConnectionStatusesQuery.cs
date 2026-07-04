using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetConnectionStatusesQuery : IRequest<Result<GetConnectionStatusesResponse>>
{
    public required string UserId { get; init; }
    public required string[] UserIds { get; init; }
}
