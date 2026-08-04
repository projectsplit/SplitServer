using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class UnsubscribeFromPushCommandHandler : IRequestHandler<UnsubscribeFromPushCommand, Result>
{
    private readonly IPushSubscriptionsRepository _pushSubscriptionsRepository;

    public UnsubscribeFromPushCommandHandler(IPushSubscriptionsRepository pushSubscriptionsRepository)
    {
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
    }

    public async Task<Result> Handle(UnsubscribeFromPushCommand command, CancellationToken ct)
    {
        // Deletes by endpoint alone, without checking who owns the row. An endpoint is only
        // obtainable from that device's own pushManager, so holding one is already proof of being
        // on the device, and subscribing takes the endpoint over on the same basis. Requiring
        // ownership here would instead strand exactly the rows that need clearing: a device
        // carrying a row for a previous account could never detach it, which is what leaks one
        // user's notifications to the next person to sign in on that browser.
        return await _pushSubscriptionsRepository.DeleteByEndpoint(command.Endpoint, ct);
    }
}
