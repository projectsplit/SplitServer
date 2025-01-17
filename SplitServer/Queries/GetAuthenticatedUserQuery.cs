using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetAuthenticatedUserQuery : IRequest<Result<GetAuthenticatedUserResponse>>
{
    public string UserId { get; }

    public GetAuthenticatedUserQuery(
        string userId)
    {
        UserId = userId;
    }
}