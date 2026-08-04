using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyTransfersCommandHandler : IRequestHandler<CreateManyTransfersCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;
    private readonly NotificationService _notificationService;

    public CreateManyTransfersCommandHandler(
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

    public async Task<Result> Handle(CreateManyTransfersCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (user, group, _) = permissionResult.Value;

        foreach (var t in command.Transfers)
        {
            var transferValidationResult = _validationService.ValidateTransfer(group, t.SenderId, t.ReceiverId, t.Amount, t.Currency);

            if (transferValidationResult.IsFailure)
            {
                return transferValidationResult;
            }
        }

        var now = DateTime.UtcNow;

        var transfers = command.Transfers
            .Select(x => new GroupTransfer
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                GroupId = command.GroupId,
                CreatorId = group.Members.Single(m => m.UserId == command.UserId).Id,
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

        await NotifyInvolvedMembers(command, group, user.Username, ct);

        return writeResult;
    }

    /// <summary>
    /// Settling up writes a batch of transfers at once. Each involved member is told once about the
    /// batch rather than once per transfer, so a settle-up cannot turn into a burst of
    /// notifications. That means the text stays deliberately generic: a single message goes to
    /// everyone, and quoting one transfer's amount would be wrong for most of them.
    /// </summary>
    private async Task NotifyInvolvedMembers(
        CreateManyTransfersCommand command,
        Group group,
        string creatorUsername,
        CancellationToken ct)
    {
        var recipientUserIds = GroupService.GetInvolvedUserIdsToNotify(
            group,
            command.Transfers.SelectMany(x => new[] { x.SenderId, x.ReceiverId }),
            command.UserId);

        var transferCount = command.Transfers.Count;

        await _notificationService.Notify(
            recipientUserIds,
            group.Name,
            transferCount == 1
                ? $"{creatorUsername} recorded a transfer"
                : $"{creatorUsername} recorded {transferCount} transfers",
            $"/shared/{command.GroupId}/transfers",
            ct);
    }
}