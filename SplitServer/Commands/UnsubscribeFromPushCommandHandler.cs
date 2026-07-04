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
        var subscriptionMaybe = await _pushSubscriptionsRepository.GetByEndpoint(command.Endpoint, ct);

        if (subscriptionMaybe.HasNoValue)
        {
            return Result.Success();
        }

        if (subscriptionMaybe.Value.UserId != command.UserId)
        {
            return Result.Failure("Push subscription does not belong to this user");
        }

        return await _pushSubscriptionsRepository.DeleteByEndpoint(command.Endpoint, ct);
    }
}
