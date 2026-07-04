using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Commands;

public class SubscribeToPushCommandHandler : IRequestHandler<SubscribeToPushCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPushSubscriptionsRepository _pushSubscriptionsRepository;

    public SubscribeToPushCommandHandler(
        IUsersRepository usersRepository,
        IPushSubscriptionsRepository pushSubscriptionsRepository)
    {
        _usersRepository = usersRepository;
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
    }

    public async Task<Result> Handle(SubscribeToPushCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        if (string.IsNullOrWhiteSpace(command.Endpoint) ||
            string.IsNullOrWhiteSpace(command.P256dh) ||
            string.IsNullOrWhiteSpace(command.Auth))
        {
            return Result.Failure("Push subscription is incomplete");
        }

        var now = DateTime.UtcNow;

        var existingSubscriptionMaybe = await _pushSubscriptionsRepository.GetByEndpoint(command.Endpoint, ct);

        // The same device/endpoint may be reused after logging in with a different account
        var subscription = existingSubscriptionMaybe.HasValue
            ? existingSubscriptionMaybe.Value with
            {
                UserId = command.UserId,
                P256dh = command.P256dh,
                Auth = command.Auth,
                Updated = now
            }
            : new PushSubscription
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                UserId = command.UserId,
                Endpoint = command.Endpoint,
                P256dh = command.P256dh,
                Auth = command.Auth
            };

        return await _pushSubscriptionsRepository.Upsert(subscription, ct);
    }
}
