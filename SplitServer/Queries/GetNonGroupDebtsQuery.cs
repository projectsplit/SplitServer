using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetNonGroupDebtsQuery: IRequest<Result<GetNonGroupDebtsResponse>>
{
    public required string UserId { get; init; }
}