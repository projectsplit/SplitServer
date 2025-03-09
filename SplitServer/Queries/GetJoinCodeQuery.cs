using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetJoinCodeQuery : IRequest<Result<GetJoinCodeResponse>>
{
    public required string UserId { get; init; }
    public required string Code { get; init; }
}