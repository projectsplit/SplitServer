using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupQuery : IRequest<Result<GetGroupResponse>>
{
    public string UserId { get; }
    
    public string GroupId { get; }
    
    public GetGroupQuery(
        string userId,
        string groupId)
    {
        UserId = userId;
        GroupId = groupId;
    }
}