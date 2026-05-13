using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetActiveBudgetInfoQuery : IRequest<Result<GetActiveBudgetInfoResponse>>
{
    public required string UserId { get; init; }
}