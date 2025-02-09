using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class SignInWithPasswordCommand : IRequest<Result<AuthTokensResult>>
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}