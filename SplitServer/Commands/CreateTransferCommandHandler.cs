using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Result<CreateTransferResponse>>
{
    private readonly PermissionService _permissionService;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;
    private readonly NotificationService _notificationService;

    public CreateTransferCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService,
        PermissionService permissionService,
        NotificationService notificationService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _notificationService = notificationService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateTransferCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateTransferResponse>();
        }

        var (user, group, memberId) = permissionResult.Value;

        var transferValidationResult =
            _validationService.ValidateTransfer(group, command.SenderId, command.ReceiverId, command.Amount, command.Currency);

        if (transferValidationResult.IsFailure)
        {
            return transferValidationResult.ConvertFailure<CreateTransferResponse>();
        }

        var now = DateTime.UtcNow;
        var transferId = Guid.NewGuid().ToString();

        var newTransfer = new GroupTransfer
        {
            Id = transferId,
            Created = now,
            Updated = now,
            GroupId = command.GroupId,
            CreatorId = memberId,
            SenderId = command.SenderId,
            ReceiverId = command.ReceiverId,
            Amount = command.Amount,
            Occurred = command.Occurred ?? now,
            Description = command.Description,
            Currency = command.Currency
        };

        var writeResult = await _transfersRepository.Insert(newTransfer, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateTransferResponse>();
        }

        await NotifyInvolvedMembers(command, group, user.Username, ct);

        return new CreateTransferResponse
        {
            TransferId = transferId
        };
    }

    /// <summary>
    /// Notifies the two sides of the transfer, minus whoever recorded it — being the sender or the
    /// receiver does not earn you a notification about your own entry. Delivery runs in the
    /// background, so a push outage can never fail a transfer that is already written.
    /// </summary>
    private async Task NotifyInvolvedMembers(
        CreateTransferCommand command,
        Group group,
        string creatorUsername,
        CancellationToken ct)
    {
        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            [command.SenderId, command.ReceiverId],
            command.UserId);

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            $"{creatorUsername} recorded a transfer of {command.Amount} {command.Currency}",
            $"/shared/{command.GroupId}/transfers",
            ct);
    }
}