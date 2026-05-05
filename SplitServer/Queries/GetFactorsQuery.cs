using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Queries;

public class GetFactorsQuery : IRequest<Result<FactorsResponse>>
{
    public required string UserId { get; init; }
}
