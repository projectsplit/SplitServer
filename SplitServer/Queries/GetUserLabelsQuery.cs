using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetUserLabelsQuery: IRequest<Result<GetUserLabelsResponse>>
{
    public required string UserId { get; init; }
}