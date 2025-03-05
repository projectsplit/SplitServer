using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class SignInWithPasswordCommand : IRequest<Result<AuthenticationResponse>>
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}