using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class CreateGroupCommand : IRequest<Result<CreateGroupResponse>>
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string Currency { get; init; }
}