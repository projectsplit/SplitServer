using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetAuthenticatedUserQuery : IRequest<Result<GetAuthenticatedUserResponse>>
{
    public required string UserId { get; init; }
}