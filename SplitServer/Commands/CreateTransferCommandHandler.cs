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
    private readonly PushNotificationService _pushNotificationService;

    public CreateTransferCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService,
        PermissionService permissionService,
        PushNotificationService pushNotificationService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
        _permissionService = permissionService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result<CreateTransferResponse>> Handle(CreateTransferCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult.ConvertFailure<CreateTransferResponse>();
        }

        var (_, group, memberId) = permissionResult.Value;

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

        var (user, _, _) = permissionResult.Value;

        var participantUserIds = group.Members
            .Where(m => (m.Id == command.SenderId || m.Id == command.ReceiverId) && m.UserId != command.UserId)
            .Select(m => m.UserId);

        _pushNotificationService.NotifyInBackground(
            participantUserIds,
            group.Name,
            $"{user.Username} recorded a transfer of {command.Amount} {command.Currency}.",
            $"/shared/{command.GroupId}/transfers");

        return new CreateTransferResponse
        {
            TransferId = transferId
        };
    }
}