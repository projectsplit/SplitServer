using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Donations;

namespace SplitServer.Services.Plus;

/// <summary>
/// Where a Buqs Plus subscription becomes real, and where it stops being.
///
/// Three things reach this: the app saying "I just subscribed", Play saying "something happened to
/// a subscription", and the worker wondering whether anything has. None of them is believed. Each
/// is reduced to a purchase token, the token is taken to Google, and what Google says is what gets
/// written — the same security model as <see cref="DonationRecorder"/>, for the same reason. A
/// forged registration and a forged notification fail in the identical place, at the lookup.
///
/// Play reports a subscription's whole current state rather than a change to it, so this one path
/// serves a new subscription, a renewal, a cancellation, a failed payment, a refund and an expiry
/// alike. It also means running it twice is harmless: every write sets fields to what Google said
/// rather than moving them, and the mark on the user is recomputed from scratch each time.
/// </summary>
public class PlusRecorder
{
    private readonly IPlaySubscriptions _play;
    private readonly PlusCatalog _catalog;
    private readonly IPlusSubscriptionsRepository _plusSubscriptionsRepository;
    private readonly IUsersRepository _usersRepository;

    public PlusRecorder(
        IPlaySubscriptions play,
        PlusCatalog catalog,
        IPlusSubscriptionsRepository plusSubscriptionsRepository,
        IUsersRepository usersRepository)
    {
        _play = play;
        _catalog = catalog;
        _plusSubscriptionsRepository = plusSubscriptionsRepository;
        _usersRepository = usersRepository;
    }

    /// <summary>
    /// Verifies a Plus subscription with Google and brings this server's record of it, and the mark
    /// on its owner, into line with the answer.
    /// </summary>
    /// <param name="purchaseToken">Play's receipt. Meaningless until Google is asked about it.</param>
    /// <param name="expectedUserId">
    /// The signed-in caller, when there is one. Play echoes back the account id the app set when it
    /// launched the purchase, and the two are required to agree — without that anyone could post
    /// someone else's token and have Plus land on their own account.
    /// </param>
    /// <remarks>
    /// A failed <c>Result</c> means this will never be acceptable: not our product, a plan this
    /// server does not know, a token Play does not recognise, somebody else's purchase. A write
    /// that did not land throws instead, so the caller answers 500 and whoever sent the request
    /// tries again.
    /// </remarks>
    public async Task<Result> Record(string purchaseToken, string? expectedUserId, CancellationToken ct)
    {
        var purchaseResult = await _play.GetSubscriptionPurchase(purchaseToken, ct);

        if (purchaseResult.IsFailure)
        {
            return purchaseResult;
        }

        var purchase = purchaseResult.Value;

        // Play verifies any subscription bought against this app, a monthly tip included. Only the
        // one product, and only the plans this server can describe, may be read as Plus.
        if (!_catalog.IsPlusProduct(purchase.ProductId))
        {
            return Result.Failure($"{purchase.ProductId ?? "A subscription with no product"} is not Buqs Plus");
        }

        if (_catalog.FindPlan(purchase.BasePlanId) is not { } plan)
        {
            return Result.Failure($"{purchase.BasePlanId ?? "A subscription with no base plan"} is not a Buqs Plus plan");
        }

        var existingMaybe = await _plusSubscriptionsRepository.GetById(purchaseToken, ct);

        // A token a newer purchase has taken over from stays stood down, whatever Play goes on to
        // say about it. Play can keep reporting the old one as inside its paid period, and writing
        // that back would give the same person two live subscriptions on paper.
        if (existingMaybe.HasValue && existingMaybe.Value.State == PlusEntitlement.Replaced)
        {
            return Result.Success();
        }

        // A plan change or a resubscribe mints a fresh token that points back at the one it
        // replaces. Read before deciding whose this is, because a purchase made in the Play Store
        // rather than in the app can arrive with nothing else tying it to an account.
        var linkedMaybe = purchase.LinkedPurchaseToken is { } linkedToken && linkedToken != purchaseToken
            ? await _plusSubscriptionsRepository.GetById(linkedToken, ct)
            : Maybe<PlusSubscription>.None;

        // A notification carries no caller, so the row already on file — this token's, or the one
        // it replaces — is the fallback link. That is what the user id was written down for.
        var ownerOnFile = existingMaybe.HasValue
            ? existingMaybe.Value.UserId
            : linkedMaybe.HasValue
                ? linkedMaybe.Value.UserId
                : null;

        var userIdResult = PurchaseOwner.Resolve(purchase.UserId ?? ownerOnFile, expectedUserId);

        if (userIdResult.IsFailure)
        {
            return userIdResult;
        }

        var userId = userIdResult.Value;
        var now = DateTime.UtcNow;

        var subscription = new PlusSubscription
        {
            Id = purchaseToken,
            UserId = userId,
            ProductId = _catalog.ProductId,
            BasePlanId = purchase.BasePlanId!,
            Plan = plan,
            State = purchase.State,
            ExpiresAt = purchase.ExpiresAt,
            AutoRenewing = purchase.AutoRenewing,
            RenewalPrice = purchase.RenewalPrice,
            RenewalCurrency = purchase.RenewalCurrency?.ToUpperInvariant(),
            LatestOrderId = purchase.LatestOrderId,
            StartedAt = purchase.StartedAt,
            // Both carried over on a replay: when it was first seen, and which renewal has already
            // been announced. Losing the second would announce the same renewal again.
            RenewalReminderSentFor = existingMaybe.HasValue ? existingMaybe.Value.RenewalReminderSentFor : null,
            Created = existingMaybe.HasValue ? existingMaybe.Value.Created : now,
            Updated = now,
        };

        EnsureWritten(await _plusSubscriptionsRepository.Upsert(subscription, ct), "Plus subscription");

        if (linkedMaybe.HasValue && !PlusEntitlement.HasEnded(linkedMaybe.Value.State))
        {
            var replaced = linkedMaybe.Value with { State = PlusEntitlement.Replaced, AutoRenewing = false, Updated = now };

            EnsureWritten(await _plusSubscriptionsRepository.Upsert(replaced, ct), "Plus subscription");

            // One Google account can pay while signed in to two different Buqs accounts. If the
            // subscription this replaces was credited to somebody else, their mark has just become
            // untrue as well, and nothing else would ever come back to correct it.
            if (linkedMaybe.Value.UserId != userId)
            {
                await RefreshUser(linkedMaybe.Value.UserId, ct);
            }
        }

        await RefreshUser(userId, ct);

        // Last, and only once everything above has landed. Play refunds a subscription that goes
        // three days unacknowledged, so this is what makes the purchase stick — and acknowledging
        // before the record existed would let the money stay for a subscription nobody was given.
        // Only for a purchase that has been paid: one whose payment is still pending is recorded
        // and left, and is acknowledged on the pass that finds the money has arrived.
        if (purchase.NeedsAcknowledgement && PlusEntitlement.IsPaidFor(purchase.State))
        {
            var acknowledgeResult = await _play.AcknowledgeSubscription(_catalog.ProductId, purchaseToken, ct);

            if (acknowledgeResult.IsFailure)
            {
                // Thrown rather than returned. The subscription is recorded and the payment is
                // real, so this must come back as a 500 and be retried; swallowing it would let
                // Play refund something this server has already granted.
                throw new InvalidOperationException(
                    $"Recorded a Plus subscription but could not acknowledge it: {acknowledgeResult.Error}");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Rewrites the Plus mark on one user from their subscription rows and the clock, without
    /// asking Google anything.
    /// </summary>
    /// <remarks>
    /// The mark is always worked out from every row the person has, never nudged from the one that
    /// just changed. Someone who resubscribes has an old row ending and a new one beginning, and
    /// whichever of the two is processed last must leave the same answer behind.
    /// </remarks>
    public async Task RefreshUser(string userId, CancellationToken ct)
    {
        var subscriptions = await _plusSubscriptionsRepository.GetByUserId(userId, ct);

        var plus = PlusEntitlement.ToUserPlus(PlusEntitlement.Current(subscriptions, DateTime.UtcNow));

        EnsureWritten(await _usersRepository.SetPlus(userId, plus, ct), "the Plus mark on a user");
    }

    /// <summary>
    /// Turns a write that did not land into an exception, so the caller answers 500 and the sender
    /// tries again. Swallowing it would leave a subscription Play is charging for with no record on
    /// this side, and nothing with any reason to retry.
    /// </summary>
    private static void EnsureWritten(Result result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to write {what} while recording a Plus subscription: {result.Error}");
        }
    }
}
