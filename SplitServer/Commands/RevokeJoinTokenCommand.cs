using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class RevokeJoinTokenCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string JoinToken { get; init; }
}