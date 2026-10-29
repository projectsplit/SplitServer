using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ChangePasswordCommand : IRequest<Result>
{
    public required string UserId { get; init; }

    /// <summary>
    /// The session the request came from: the one that stays signed in once every other one is
    /// dropped.
    /// </summary>
    public required string SessionId { get; init; }

    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}
