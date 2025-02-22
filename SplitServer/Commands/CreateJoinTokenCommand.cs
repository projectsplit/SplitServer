using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class CreateJoinTokenCommand : IRequest<Result<CreateJoinTokenResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}