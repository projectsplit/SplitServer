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
        if (string.IsNullOrWhiteSpace(command.Endpoint) ||
            string.IsNullOrWhiteSpace(command.P256dh) ||
            string.IsNullOrWhiteSpace(command.Auth))
        {
            return Result.Failure("Push subscription is incomplete");
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var now = DateTime.UtcNow;

        // The endpoint is the real identity of a device subscription, so whatever is already stored
        // for it gets replaced wholesale. Two things fall out of that:
        //
        // An endpoint identifies a browser install, not a person, so the same one comes back when a
        // second account signs in on this device — replacing stops the previous user from carrying
        // on receiving notifications here.
        //
        // And deleting *many* rather than one collapses any duplicate rows a client that subscribed
        // twice concurrently may have left, which otherwise deliver the same notification twice.
        var deleteResult = await _pushSubscriptionsRepository.DeleteByEndpoint(command.Endpoint, ct);

        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        var subscription = new PushSubscription
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = command.UserId,
            Endpoint = command.Endpoint,
            P256dh = command.P256dh,
            Auth = command.Auth,
        };

        return await _pushSubscriptionsRepository.Insert(subscription, ct);
    }
}
