using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class SignUpWithPasswordCommand : IRequest<Result<AuthenticationResponse>>
{
    public required string Password { get; init; }
    public required string Username { get; init; }
}