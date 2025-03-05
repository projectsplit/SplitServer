using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetAuthenticatedUserQuery : IRequest<Result<GetAuthenticatedUserResponse>>
{
    public required string UserId { get; init; }
}