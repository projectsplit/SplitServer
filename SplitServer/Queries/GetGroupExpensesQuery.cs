using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupExpensesQuery : IRequest<Result<GetGroupExpensesResponse>>
{
    public string UserId { get; }
    public string GroupId { get; }
    public int PageSize { get; }
    public string? Next { get; }

    public GetGroupExpensesQuery(
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