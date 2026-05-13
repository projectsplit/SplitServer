using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetInactiveBudgetsInfoQuery : IRequest<Result<GetInactiveBudgetsInfoResponse>>
{
    public required string UserId { get; init; }
}