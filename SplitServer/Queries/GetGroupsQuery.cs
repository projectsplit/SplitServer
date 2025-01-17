using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetGroupsQuery : IRequest<Result<GetGroupsResponse>>
{
    public string UserId { get; }
    
    public int PageSize { get; }
    
    public string? Next { get;}
    
    public GetGroupsQuery(
        string userId,
        int pageSize,
        string? next)
    {
        UserId = userId;
        PageSize = pageSize;
        Next = next;
    }
}