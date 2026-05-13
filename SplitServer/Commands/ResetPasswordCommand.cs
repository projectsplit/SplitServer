using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ResetPasswordCommand : IRequest<Result>
{
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}
