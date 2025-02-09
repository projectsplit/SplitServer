using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupQuery : IRequest<Result<GetGroupResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}