using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetMostRecentEngineSetupQuery : IRequest<Result<GetMostRecentEngineSetupResponse>>
{
    public required string UserId { get; init; }
}
