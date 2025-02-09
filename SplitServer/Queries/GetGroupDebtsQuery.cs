using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupDebtsQuery : IRequest<Result<GetGroupDebtsResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}