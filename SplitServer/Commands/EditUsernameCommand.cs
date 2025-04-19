using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class EditUsernameCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
}