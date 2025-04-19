using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUsernameStatusQuery : IRequest<Result<GetUsernameStatusResponse>>
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
}