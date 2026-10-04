using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;
using SplitServer.Services.Donations;

namespace SplitServer.Commands;

/// <summary>
/// Deletes an account and everything that belongs to it alone.
///
/// The line drawn here is between what is the person's and what they shared. Their own records go:
/// the account, its sessions, preferences, budgets, labels, notifications, personal expenses, and
/// any group nobody else with an account is in. What they shared stays for as long as someone else
/// can still see it, because it is that person's record too — a group keeps its ledger with the
/// departed member turned into a guest, as if they had left, and an expense split directly with
/// someone stays on that person's side, where it shows a deleted user.
///
/// Nothing here runs in a transaction, so the order is what makes a failure recoverable. Every step
/// is safe to repeat, and the account itself goes last: if anything fails part-way, the person can
/// still sign in and ask again.
/// </summary>
public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly ITransfersRepository _transfersRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly IJoinCodesRepository _joinCodesRepository;
    private readonly IUserActivityRepository _userActivityRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IUserLabelsRepository _userLabelsRepository;
    private readonly IBudgetsRepository _budgetsRepository;
    private readonly IEmailVerificationCodesRepository _emailVerificationCodesRepository;
    private readonly IPushSubscriptionsRepository _pushSubscriptionsRepository;
    private readonly INotificationsRepository _notificationsRepository;
    private readonly IUserConnectionsRepository _userConnectionsRepository;
    private readonly IDonationSubscriptionsRepository _donationSubscriptionsRepository;
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;
    private readonly GooglePlayBillingService _play;

    public DeleteAccountCommandHandler(
        IUsersRepository usersRepository,
        ISessionsRepository sessionsRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository,
        IRecurringExpensesRepository recurringExpensesRepository,
        ITransfersRepository transfersRepository,
        IInvitationsRepository invitationsRepository,
        IJoinCodesRepository joinCodesRepository,
        IUserActivityRepository userActivityRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IUserLabelsRepository userLabelsRepository,
        IBudgetsRepository budgetsRepository,
        IEmailVerificationCodesRepository emailVerificationCodesRepository,
        IPushSubscriptionsRepository pushSubscriptionsRepository,
        INotificationsRepository notificationsRepository,
        IUserConnectionsRepository userConnectionsRepository,
        IDonationSubscriptionsRepository donationSubscriptionsRepository,
        IDonationPromptStatesRepository donationPromptStatesRepository,
        GooglePlayBillingService play)
    {
        _usersRepository = usersRepository;
        _sessionsRepository = sessionsRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
        _recurringExpensesRepository = recurringExpensesRepository;
        _transfersRepository = transfersRepository;
        _invitationsRepository = invitationsRepository;
        _joinCodesRepository = joinCodesRepository;
        _userActivityRepository = userActivityRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _userLabelsRepository = userLabelsRepository;
        _budgetsRepository = budgetsRepository;
        _emailVerificationCodesRepository = emailVerificationCodesRepository;
        _pushSubscriptionsRepository = pushSubscriptionsRepository;
        _notificationsRepository = notificationsRepository;
        _userConnectionsRepository = userConnectionsRepository;
        _donationSubscriptionsRepository = donationSubscriptionsRepository;
        _donationPromptStatesRepository = donationPromptStatesRepository;
        _play = play;
    }

    public async Task<Result> Handle(DeleteAccountCommand command, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        // First, because it is the only step that can refuse, and refusing with half the account
        // already gone would be the worst of both.
        var stopGiftsResult = await StopMonthlyGifts(user.Id, ct);

        if (stopGiftsResult.IsFailure)
        {
            return stopGiftsResult;
        }

        var groups = await _groupsRepository.GetAllByUserId(user.Id, ct);

        foreach (var group in groups)
        {
            var leaveResult = await LeaveGroup(group, user, ct);

            if (leaveResult.IsFailure)
            {
                return leaveResult;
            }
        }

        var templates = await _recurringExpensesRepository.GetAllByUserId(user.Id, ct);

        // Before the templates go rather than after, the reverse of deleting a single one. Run the
        // other way round, a failure in between would leave shared expenses pointing at schedules
        // that a second attempt could no longer find to clean up after.
        foreach (var template in templates)
        {
            var clearResult = await _expensesRepository.ClearRecurringExpenseId(template.Id, ct);

            if (clearResult.IsFailure)
            {
                return clearResult;
            }
        }

        var orphansResult = await DeleteRecordsLeftWithNobody(user.Id, ct);

        if (orphansResult.IsFailure)
        {
            return orphansResult;
        }

        return await RunInOrder(
        [
            () => _recurringExpensesRepository.DeleteByUserId(user.Id, ct),
            () => _expensesRepository.DeletePersonalByCreatorId(user.Id, ct),
            () => _invitationsRepository.DeleteByUserId(user.Id, ct),
            () => _userConnectionsRepository.DeleteByUserId(user.Id, ct),
            () => _userLabelsRepository.DeleteByUserId(user.Id, ct),
            () => _budgetsRepository.DeleteByUserId(user.Id, ct),
            () => _emailVerificationCodesRepository.DeleteByUserId(user.Id, ct),
            () => _notificationsRepository.DeleteByUserId(user.Id, ct),
            () => _pushSubscriptionsRepository.DeleteByUserId(user.Id, ct),
            () => _donationPromptStatesRepository.Delete(user.Id, ct),
            () => _userPreferencesRepository.Delete(user.Id, ct),
            () => _userActivityRepository.Delete(user.Id, ct),
            // Signs every device out: with no session left, no refresh can succeed.
            () => _sessionsRepository.DeleteByUserId(user.Id, ct),
            () => _usersRepository.Delete(user.Id, ct),
        ]);
    }

    /// <summary>
    /// Stops any monthly gift before the account it belongs to disappears. Play would otherwise go
    /// on charging someone for supporting an app they no longer have an account with, and with the
    /// account gone there would be nothing left here to notice.
    ///
    /// The donation records themselves are kept. They are a ledger of payments received, and they
    /// name nobody once the account they point at no longer exists.
    /// </summary>
    private async Task<Result> StopMonthlyGifts(string userId, CancellationToken ct)
    {
        var subscriptions = await _donationSubscriptionsRepository.GetByUserId(userId, ct);

        foreach (var subscription in subscriptions)
        {
            // Asked afresh rather than read off the row. The row only changes when a notification
            // arrives, and someone who has just cancelled in Google Play must not be told to do it
            // again because ours has not caught up.
            var purchaseResult = await _play.GetSubscriptionPurchase(subscription.Id, ct);

            // A token Play does not recognise cannot be charged either.
            if (purchaseResult.IsFailure || purchaseResult.Value.HasEnded)
            {
                continue;
            }

            var cancelResult = await _play.CancelSubscription(subscription.Id, ct);

            if (cancelResult.IsFailure)
            {
                return Result.Failure(
                    "Your monthly contribution could not be cancelled. Please cancel it in Google Play, then try again.");
            }
        }

        return Result.Success();
    }

    private async Task<Result> DeleteRecordsLeftWithNobody(string userId, CancellationToken ct)
    {
        var expenses = await _expensesRepository.GetNonGroupExpensesByUserId(userId, ct: ct);
        var transfers = await _transfersRepository.GetAllByUserId(userId, ct);

        var otherUserIds = OrphanedNonGroupRecords.OtherUserIds(expenses, transfers, userId);

        var existingUserIds = (await _usersRepository.GetByIds(otherUserIds, ct))
            .Select(x => x.Id)
            .ToHashSet();

        var expenseIds = OrphanedNonGroupRecords.ExpenseIds(expenses, userId, existingUserIds);
        var transferIds = OrphanedNonGroupRecords.TransferIds(transfers, userId, existingUserIds);

        if (expenseIds.Count > 0)
        {
            var deleteExpensesResult = await _expensesRepository.DeleteByIds(expenseIds, ct);

            if (deleteExpensesResult.IsFailure)
            {
                return deleteExpensesResult;
            }
        }

        return transferIds.Count > 0
            ? await _transfersRepository.DeleteByIds(transferIds, ct)
            : Result.Success();
    }

    private async Task<Result> LeaveGroup(Group group, User user, CancellationToken ct)
    {
        var member = group.Members.First(x => x.UserId == user.Id);

        var memberHasActivity =
            await _expensesRepository.ExistsInAnyExpense(group.Id, member.Id, ct) ||
            await _transfersRepository.ExistsInAnyTransfer(group.Id, member.Id, ct);

        var remainingGroup = GroupDeparture.WithoutMember(group, member, user.Username, memberHasActivity, DateTime.UtcNow);

        return remainingGroup is not null
            ? await _groupsRepository.Update(remainingGroup, ct)
            : await DeleteGroup(group.Id, ct);
    }

    /// <summary>
    /// Removes a group and everything in it. The group itself goes last: it is how the next attempt
    /// finds its way back here, so deleting it first would strand whatever a failure left behind.
    /// </summary>
    private async Task<Result> DeleteGroup(string groupId, CancellationToken ct)
    {
        return await RunInOrder(
        [
            () => _expensesRepository.DeleteByGroupId(groupId, ct),
            () => _recurringExpensesRepository.DeleteByGroupId(groupId, ct),
            () => _transfersRepository.DeleteByGroupId(groupId, ct),
            () => _invitationsRepository.DeleteByGroupId(groupId, ct),
            () => _joinCodesRepository.DeleteByGroupId(groupId, ct),
            () => _userActivityRepository.ClearRecentGroupForAllUsers(groupId, DateTime.UtcNow, ct),
            () => _groupsRepository.Delete(groupId, ct),
        ]);
    }

    private static async Task<Result> RunInOrder(Func<Task<Result>>[] steps)
    {
        foreach (var step in steps)
        {
            var result = await step();

            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
