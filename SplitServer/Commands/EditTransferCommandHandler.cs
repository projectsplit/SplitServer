using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class EditTransferCommandHandler : IRequestHandler<EditTransferCommand, Result>
{
    private readonly ITransfersRepository _transfersRepository;
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly NotificationService _notificationService;

    public EditTransferCommandHandler(
        ITransfersRepository transfersRepository,
        PermissionService permissionService,
        ValidationService validationService,
        NotificationService notificationService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(EditTransferCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyTransferAction(command.UserId, command.TransferId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, group, transfer, _) = permissionResult.Value;

        var transferValidationResult =
            _validationService.ValidateTransfer(group, command.SenderId, command.ReceiverId, command.Amount, command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult;
        }

        var now = DateTime.UtcNow;

        var editedTransfer = transfer with
        {
            Updated = now,
            SenderId = command.SenderId,
            ReceiverId = command.ReceiverId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency
        };

        var updateResult = await _transfersRepository.Update(editedTransfer, ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        // Only money movements are worth a notification, so retitling or redating a transfer tells
        // nobody. Swapping a party counts: it moves the same amount between different people.
        var partiesChanged =
            transfer.SenderId != command.SenderId || transfer.ReceiverId != command.ReceiverId;

        var amountChanged =
            transfer.Amount != command.Amount || transfer.Currency != command.Currency;

        if (!partiesChanged && !amountChanged)
        {
            return updateResult;
        }

        // Both sides either side of the edit, so a party dropped from the transfer is told as well.
        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            [transfer.SenderId, transfer.ReceiverId, command.SenderId, command.ReceiverId],
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{user.Username} edited a transfer of {command.Amount} {command.Currency}",
            $"/shared/{group.Id}/transfers",
            ct);

        return updateResult;
    }
}