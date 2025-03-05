using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupQuery : IRequest<Result<GetGroupResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}