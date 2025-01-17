using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupDebtsQuery : IRequest<Result<GetGroupDebtsResponse>>
{
    public string UserId { get; }
    public string GroupId { get; }

    public GetGroupDebtsQuery(
        string userId,
        string groupId)
    {
        UserId = userId;
        GroupId = groupId;
    }
}