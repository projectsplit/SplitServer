using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Queries;

public class GetAllGroupsTotalBalancesQuery : IRequest<Result<GetAllGroupsTotalBalancesResponse>>
{
    public required string UserId { get; init; }
}