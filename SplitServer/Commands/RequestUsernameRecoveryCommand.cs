using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RequestUsernameRecoveryCommand : IRequest<Result>
{
    public required string Email { get; init; }
}
