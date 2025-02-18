using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupDetailsQuery : IRequest<Result<GetGroupDetailsResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}