using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Extensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

/// <summary>
/// Puts the receiver into the group the invitation is for. As a new member when the invitation
/// names no guest. Onto the guest's slot when it does: a newcomer takes the slot over, and someone
/// already in the group is merged into it (see <see cref="GuestMerge"/>).
/// </summary>
public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Result>
{
    // A group write only loses to another write to the same group in the same moment; reading the
    // group again and going once more is nearly always enough.
    private const int MaxAttempts = 3;

    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;

    public AcceptInvitationCommandHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IInvitationsRepository invitationsRepository,
        IExpensesRepository expensesRepository,
        ITransfersRepository transfersRepository,
        IRecurringExpensesRepository recurringExpensesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _invitationsRepository = invitationsRepository;
        _expensesRepository = expensesRepository;
        _transfersRepository = transfersRepository;
        _recurringExpensesRepository = recurringExpensesRepository;
    }

    public async Task<Result> Handle(AcceptInvitationCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var invitationMaybe = await _invitationsRepository.GetById(command.InvitationId, ct);

        if (invitationMaybe.HasNoValue)
        {
            return Result.Failure($"Invitation with id {command.InvitationId} was not found");
        }

        var invitation = invitationMaybe.Value;

        if (invitation.ReceiverId != command.UserId)
        {
            return Result.Failure("You cannot accept this invitation");
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var groupMaybe = await _groupsRepository.GetById(invitation.GroupId, ct);

            if (groupMaybe.HasNoValue)
            {
                return Result.Failure($"Group with id {invitation.GroupId} was not found");
            }

            var acceptResult = await TryAccept(invitation, groupMaybe.Value, ct);

            if (acceptResult.IsFailure)
            {
                return Result.Failure(acceptResult.Error);
            }

            if (acceptResult.Value)
            {
                return Result.Success();
            }
        }

        return Result.Failure(GroupsRepositoryExtensions.ChangedMeanwhileError);
    }

    /// <returns>
    /// True once the receiver is in the group and the invitation is gone. False when the group
    /// changed under the write, so everything has to be decided again from a fresh copy.
    /// </returns>
    private async Task<Result<bool>> TryAccept(Invitation invitation, Group group, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var member = group.Members.FirstOrDefault(x => x.UserId == invitation.ReceiverId);

        if (invitation.GuestId is null)
        {
            if (member is not null)
            {
                return Result.Failure<bool>("User is already a group member");
            }

            var newMember = new Member { Id = Guid.NewGuid().ToString(), UserId = invitation.ReceiverId, Joined = now };

            var saveResult = await _groupsRepository.UpdateIfUnchanged(
                group with { Members = group.Members.Append(newMember).ToList(), Updated = now },
                group.Updated,
                ct);

            if (saveResult.IsFailure || !saveResult.Value)
            {
                return saveResult;
            }

            return await Finished(_invitationsRepository.Delete(invitation.Id, ct));
        }

        if (member?.Id == invitation.GuestId)
        {
            // The group write landed on an earlier attempt that did not get to finish. Only what
            // comes after it is left to do.
            return await Finished(FinishReplacing(invitation, group.Id, ct));
        }

        var guest = group.Guests.FirstOrDefault(x => x.Id == invitation.GuestId);

        if (guest is null)
        {
            // Someone else took the slot first, or the guest was removed. Nothing is left to accept.
            var deleteResult = await _invitationsRepository.Delete(invitation.Id, ct);

            return deleteResult.IsFailure
                ? deleteResult.ConvertFailure<bool>()
                : Result.Failure<bool>("This guest has already been replaced");
        }

        if (member is null)
        {
            var saveResult = await _groupsRepository.UpdateIfUnchanged(
                GuestMerge.TakeOver(group, guest, invitation.ReceiverId, now),
                group.Updated,
                ct);

            if (saveResult.IsFailure || !saveResult.Value)
            {
                return saveResult;
            }

            // A retired id noted on an earlier attempt belonged to a membership this person has
            // since given up; a takeover has no records of theirs to rewrite.
            return await Finished(FinishReplacing(invitation with { ReplacedMemberId = null }, group.Id, ct));
        }

        // The journal goes first: after the group write, the member's old id survives nowhere else.
        if (invitation.ReplacedMemberId != member.Id)
        {
            invitation = invitation with { ReplacedMemberId = member.Id, Updated = now };

            var journalResult = await _invitationsRepository.Update(invitation, ct);

            if (journalResult.IsFailure)
            {
                return journalResult.ConvertFailure<bool>();
            }
        }

        var claimResult = await _groupsRepository.UpdateIfUnchanged(
            GuestMerge.Claim(group, member, guest, now),
            group.Updated,
            ct);

        if (claimResult.IsFailure || !claimResult.Value)
        {
            return claimResult;
        }

        return await Finished(FinishReplacing(invitation, group.Id, ct));
    }

    /// <summary>
    /// What comes after the group write of a replacement: the records of a retired id are rewritten
    /// to the guest's, and every invitation onto that guest goes — this one, and any other still
    /// waiting for a slot that no longer exists.
    /// </summary>
    private async Task<Result> FinishReplacing(Invitation invitation, string groupId, CancellationToken ct)
    {
        if (invitation.ReplacedMemberId is not null)
        {
            var rewriteResult = await RewriteRecords(groupId, invitation.ReplacedMemberId, invitation.GuestId!, ct);

            if (rewriteResult.IsFailure)
            {
                return rewriteResult;
            }
        }

        return await _invitationsRepository.DeleteByGuestId(invitation.GuestId!, groupId, ct);
    }

    private async Task<Result> RewriteRecords(string groupId, string retiredId, string keptId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        foreach (var expense in await _expensesRepository.GetGroupExpensesInvolving(groupId, retiredId, ct))
        {
            var updateResult = await _expensesRepository.Update(GuestMerge.Rewrite(expense, retiredId, keptId, now), ct);

            if (updateResult.IsFailure)
            {
                return updateResult;
            }
        }

        foreach (var transfer in await _transfersRepository.GetGroupTransfersInvolving(groupId, retiredId, ct))
        {
            var rewritten = GuestMerge.Rewrite(transfer, retiredId, keptId, now);

            var writeResult = rewritten is null
                ? await _transfersRepository.Delete(transfer.Id, ct)
                : await _transfersRepository.Update(rewritten, ct);

            if (writeResult.IsFailure)
            {
                return writeResult;
            }
        }

        foreach (var template in await _recurringExpensesRepository.GetGroupTemplatesInvolving(groupId, retiredId, ct))
        {
            var rewriteResult = await RewriteTemplate(template, retiredId, keptId, now, ct);

            if (rewriteResult.IsFailure)
            {
                return rewriteResult;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Templates are written conditionally because the worker may be advancing one this very
    /// moment. A plain replace from here could put back a next-occurrence date the worker had just
    /// moved past, and its next pass would fire the same occurrence twice. On a clash the fresh copy
    /// is rewritten instead.
    /// </summary>
    private async Task<Result> RewriteTemplate(
        GroupRecurringExpense template,
        string retiredId,
        string keptId,
        DateTime now,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var updateResult = await _recurringExpensesRepository.UpdateIfUnchanged(
                GuestMerge.Rewrite(template, retiredId, keptId, now),
                template.Updated,
                ct);

            if (updateResult.IsFailure)
            {
                return Result.Failure(updateResult.Error);
            }

            if (updateResult.Value)
            {
                return Result.Success();
            }

            var freshMaybe = await _recurringExpensesRepository.GetById(template.Id, ct);

            if (freshMaybe.HasNoValue || freshMaybe.Value is not GroupRecurringExpense fresh)
            {
                // Deleted meanwhile. Nothing is left to rewrite.
                return Result.Success();
            }

            template = fresh;
        }

        return Result.Failure($"Recurring expense {template.Id} kept changing while it was being rewritten");
    }

    private static async Task<Result<bool>> Finished(Task<Result> finishing)
    {
        var result = await finishing;

        return result.IsFailure ? result.ConvertFailure<bool>() : Result.Success(true);
    }
}
