using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupDetailsQuery : IRequest<Result<GetGroupDetailsResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}