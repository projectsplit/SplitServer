using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RequestPasswordResetCommand : IRequest<Result>
{
    public required string Email { get; init; }
}
