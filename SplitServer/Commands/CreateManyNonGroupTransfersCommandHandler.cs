using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyNonGroupTransfersCommandHandler : IRequestHandler<CreateManyNonGroupTransfersCommand, Result>
{
    private const string NonGroupTransfersUrl = "/shared/nongroup/transfers";

    private readonly ITransfersRepository _transfersRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;
    private readonly NotificationService _notificationService;

    public CreateManyNonGroupTransfersCommandHandler(
        ITransfersRepository transfersRepository,
        IUsersRepository usersRepository,
        ValidationService validationService,
        NotificationService notificationService)
    {
        _transfersRepository = transfersRepository;
        _usersRepository = usersRepository;
        _validationService = validationService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(CreateManyNonGroupTransfersCommand command, CancellationToken ct)
    {
        foreach (var t in command.Transfers)
        {
            var transferValidationResult = _validationService.ValidateNonGroupTransfer(
                t.SenderId,
                t.ReceiverId,
                command.UserId,
                t.Amount,
                t.Currency);

            if (transferValidationResult.IsFailure)
            {
                return transferValidationResult;
            }
        }

        var now = DateTime.UtcNow;

        var transfers = command.Transfers
            .Select(x => new NonGroupTransfer
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                CreatorId = command.UserId,
                SenderId = x.SenderId,
                ReceiverId = x.ReceiverId,
                Amount = x.Amount,
                Currency = x.Currency,
                Description = x.Description,
                Occurred = x.Occurred ?? now,
            })
            .ToList();

        var writeResult = await _transfersRepository.InsertMany(transfers, ct);

        if (writeResult.IsFailure)
        {
            return writeResult;
        }

        await NotifyCounterparties(command, ct);

        return writeResult;
    }

    /// <summary>
    /// One message per counterparty for the whole batch, not one per transfer, so settling up
    /// cannot turn into a burst of notifications. The creator is dropped even though they are a
    /// party to every transfer here.
    /// </summary>
    private async Task NotifyCounterparties(CreateManyNonGroupTransfersCommand command, CancellationToken ct)
    {
        var recipientUserIds = command.Transfers
            .SelectMany(x => new[] { x.SenderId, x.ReceiverId })
            .Where(x => x != command.UserId)
            .Distinct()
            .ToList();

        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var creatorMaybe = await _usersRepository.GetById(command.UserId, ct);

        var creatorUsername = creatorMaybe.HasValue ? creatorMaybe.Value.Username : "Someone";

        var transferCount = command.Transfers.Count;

        await _notificationService.Notify(
            recipientUserIds,
            "New transfer",
            transferCount == 1
                ? $"{creatorUsername} recorded a transfer with you"
                : $"{creatorUsername} recorded {transferCount} transfers with you",
            NonGroupTransfersUrl,
            ct);
    }
}