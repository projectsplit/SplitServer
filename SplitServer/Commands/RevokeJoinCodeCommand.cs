using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RevokeJoinCodeCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Code { get; init; }
}