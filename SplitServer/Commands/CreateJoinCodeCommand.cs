using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

public class CreateJoinCodeCommand : IRequest<Result<CreateJoinCodeResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}