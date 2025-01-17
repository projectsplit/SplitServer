using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class SignUpWithPasswordCommand : IRequest<Result<AuthTokensResult>>
{
    public required string Password { get; init; }

    public required string Username { get; init; }
}