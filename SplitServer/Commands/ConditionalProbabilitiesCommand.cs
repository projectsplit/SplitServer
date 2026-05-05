using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ConditionalProbabilitiesCommand : IRequest<Result<ConditionalQueryResponse>>
{
    public required string UserId { get; init; }
    public List<Condition> Conditions { get; init; } = new();
}
