using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupTransfersQuery : IRequest<Result<GetGroupTransfersResponse>>
{
    public string UserId { get; }
    public string GroupId { get; }
    public int PageSize { get; }
    public string? Next { get; }

    public GetGroupTransfersQuery(
        string userId,
        string groupId,
        int pageSize,
        string? next)
    {
        UserId = userId;
        GroupId = groupId;
        Next = next;
        PageSize = pageSize;
    }
}