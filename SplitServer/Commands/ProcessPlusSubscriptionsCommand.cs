using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Commands;

/// <summary>
/// One pass over the Plus subscriptions that are still live: re-read the ones that are due a look
/// from Google, then tell annual subscribers whose renewal is close.
/// </summary>
public class ProcessPlusSubscriptionsCommand : IRequest<Result<ProcessPlusSubscriptionsResponse>>
{
}
