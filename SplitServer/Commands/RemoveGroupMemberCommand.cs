using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class RemoveGroupMemberCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
    public required string MemberId { get; init; }
}