using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;
namespace SplitServer.Queries;

public class GetUserAndGroupsLabelsQuery: IRequest<Result<GetUserAndGroupsLabelsResponse>>
{
    public required string UserId { get; init; }
}