using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;
using SplitServer.Repositories;

namespace SplitServer.Queries;

public class GetAuthenticatedUserQueryHandler : IRequestHandler<GetAuthenticatedUserQuery, Result<GetAuthenticatedUserResponse>>
{
    private readonly IUsersRepository _usersRepository;

    public GetAuthenticatedUserQueryHandler(
        IUsersRepository usersRepository)
    {
        _usersRepository = usersRepository;
    }

    public async Task<Result<GetAuthenticatedUserResponse>> Handle(GetAuthenticatedUserQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetAuthenticatedUserResponse>($"User with id {query.UserId} was not found");
        }

        var user = userMaybe.Value;

        return new GetAuthenticatedUserResponse
        {
            UserId = user.Id,
            Username = user.Username
        };
    }
}