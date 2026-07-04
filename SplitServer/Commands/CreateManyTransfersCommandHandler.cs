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
    private readonly PushNotificationService _pushNotificationService;

    public CreateManyTransfersCommandHandler(
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

    public async Task<Result> Handle(CreateManyTransfersCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

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

        var (user, _, _) = permissionResult.Value;

        var participantMemberIds = command.Transfers
            .SelectMany(x => new[] { x.SenderId, x.ReceiverId })
            .ToHashSet();

        var participantUserIds = group.Members
            .Where(m => participantMemberIds.Contains(m.Id) && m.UserId != command.UserId)
            .Select(m => m.UserId);

        _pushNotificationService.NotifyInBackground(
            participantUserIds,
            group.Name,
            $"{user.Username} settled up.",
            $"/shared/{command.GroupId}/debts");

        return Result.Success();
    }
}