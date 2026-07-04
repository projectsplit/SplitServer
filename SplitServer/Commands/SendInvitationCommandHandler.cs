using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class SendInvitationCommandHandler : IRequestHandler<SendInvitationCommand, Result>
{
    private readonly PermissionService _permissionService;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly PushNotificationService _pushNotificationService;

    public SendInvitationCommandHandler(
        IInvitationsRepository invitationsRepository,
        PermissionService permissionService,
        PushNotificationService pushNotificationService)
    {
        _invitationsRepository = invitationsRepository;
        _permissionService = permissionService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Result> Handle(SendInvitationCommand command, CancellationToken ct)
    {
        var permissionResult = await _permissionService.VerifyGroupAction(command.UserId, command.GroupId, ct);

        if (permissionResult.IsFailure)
        {
            return permissionResult;
        }

        var (_, group, _) = permissionResult.Value;

        if (group.Members.Any(x => x.UserId == command.ReceiverId))
        {
            return Result.Failure("User is already a group member");
        }

        if (command.GuestId is not null)
        {
            if (group.Guests.All(x => x.Id != command.GuestId))
            {
                return Result.Failure("Guest is not a group member");
            }

            var deleteByGuestResult = await _invitationsRepository.DeleteByGuestId(command.GuestId, command.GroupId, ct);

            if (deleteByGuestResult.IsFailure)
            {
                return deleteByGuestResult;
            }
        }

        var deleteExistingResult = await _invitationsRepository.DeleteByGroupIdAndReceiverId(command.ReceiverId, command.GroupId, ct);

        if (deleteExistingResult.IsFailure)
        {
            return deleteExistingResult;
        }

        var now = DateTime.UtcNow;

        var newInvitation = new Invitation
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            SenderId = command.UserId,
            ReceiverId = command.ReceiverId,
            GroupId = command.GroupId,
            GuestId = command.GuestId,
        };

        var insertResult = await _invitationsRepository.Insert(newInvitation, ct);

        if (insertResult.IsFailure)
        {
            return insertResult;
        }

        var (user, _, _) = permissionResult.Value;

        _pushNotificationService.NotifyInBackground(
            [command.ReceiverId],
            "Group invitation",
            $"{user.Username} invited you to join \"{group.Name}\".");

        return Result.Success();
    }
}