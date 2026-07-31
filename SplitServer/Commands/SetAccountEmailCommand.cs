using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetAccountEmailCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
}
