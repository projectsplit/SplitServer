using CSharpFunctionalExtensions;
using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services.Donations;

/// <summary>
/// Where a donation actually becomes real.
///
/// Two things reach this: the app saying "I just bought something", and Play saying "something
/// happened to a purchase". Neither is believed. Both are reduced to a purchase token, and the
/// token is taken to Google, and what Google says is what gets written. That is the whole security
/// model, and it is why the same code serves both — a forged notification and a forged registration
/// fail in the identical place, at the lookup.
///
/// Every write here has to survive being run again. Play retries notifications, the app retries
/// registrations after a dropped connection, and both can arrive for the same purchase. Each write
/// is keyed by a Play identifier and sets fields to values rather than incrementing them, so a
/// replay overwrites the row it wrote the first time instead of counting a second gift.
/// </summary>
public class DonationRecorder
{
    private readonly GooglePlayBillingService _play;
    private readonly DonationCatalog _catalog;
    private readonly IDonationsRepository _donationsRepository;
    private readonly IDonationSubscriptionsRepository _donationSubscriptionsRepository;
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;

    public DonationRecorder(
        GooglePlayBillingService play,
        DonationCatalog catalog,
        IDonationsRepository donationsRepository,
        IDonationSubscriptionsRepository donationSubscriptionsRepository,
        IDonationPromptStatesRepository donationPromptStatesRepository)
    {
        _play = play;
        _catalog = catalog;
        _donationsRepository = donationsRepository;
        _donationSubscriptionsRepository = donationSubscriptionsRepository;
        _donationPromptStatesRepository = donationPromptStatesRepository;
    }

    /// <summary>
    /// Verifies and records a one-off gift.
    /// </summary>
    /// <param name="expectedUserId">
    /// The signed-in caller, when there is one. Play echoes back the account id the app set when it
    /// launched the purchase, and the two are required to agree — without that check anyone could
    /// post someone else's purchase token and have the gift credited to themselves.
    /// </param>
    public async Task<Result> RecordProductPurchase(
        string productId,
        string purchaseToken,
        string? expectedUserId,
        CancellationToken ct)
    {
        var product = _catalog.Find(productId);

        if (product is null || product.Kind != DonationKind.OneTime)
        {
            return Result.Failure($"{productId} is not a one-off contribution");
        }

        var purchaseResult = await _play.GetProductPurchase(productId, purchaseToken, ct);

        if (purchaseResult.IsFailure)
        {
            return purchaseResult;
        }

        var purchase = purchaseResult.Value;

        var userIdResult = ResolveUserId(purchase.UserId, expectedUserId);

        if (userIdResult.IsFailure)
        {
            return userIdResult;
        }

        var userId = userIdResult.Value;

        // Cards settle inside the sheet, but a delayed method — cash at a kiosk, direct debit —
        // leaves a real purchase whose money has not arrived. Recording it as pending keeps the row
        // honest until a later notification says otherwise, and holds back the thank-you.
        var status = purchase.IsPaid
            ? DonationStatus.Succeeded
            : purchase.IsPending
                ? DonationStatus.Pending
                : DonationStatus.Failed;

        await UpsertDonation(
            id: purchaseToken,
            userId: userId,
            purchaseToken: purchaseToken,
            productId: productId,
            orderId: purchase.OrderId,
            amountMinor: product.NominalAmountMinor,
            kind: DonationKind.OneTime,
            status: status,
            subscriptionId: null,
            ct);

        if (status != DonationStatus.Succeeded)
        {
            return Result.Success();
        }

        await MarkDonated(userId, hasActiveMonthly: false, ct);

        // Only after the ledger write has landed. Consuming both acknowledges the purchase — which
        // is what stops Play auto-refunding it after three days — and hands the tier back so the
        // same amount can be given again, which for a donation is the entire point.
        if (!purchase.IsConsumed)
        {
            var consumeResult = await _play.ConsumeProduct(productId, purchaseToken, ct);

            if (consumeResult.IsFailure)
            {
                // Thrown rather than returned. The gift is recorded and the money is real, so this
                // must come back as a 500 and be retried; swallowing it would let Play refund a
                // payment this server has already credited.
                throw new InvalidOperationException(
                    $"Recorded donation {purchaseToken} but could not consume it: {consumeResult.Error}");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Verifies and records a monthly gift, whether this is the first month or the ninth. Play
    /// reports the subscription's whole current state rather than a delta, so the same call handles
    /// a new subscription, a renewal, a cancellation and an expiry.
    /// </summary>
    public async Task<Result> RecordSubscriptionPurchase(
        string purchaseToken,
        string? expectedUserId,
        CancellationToken ct)
    {
        var purchaseResult = await _play.GetSubscriptionPurchase(purchaseToken, ct);

        if (purchaseResult.IsFailure)
        {
            return purchaseResult;
        }

        var purchase = purchaseResult.Value;

        if (purchase.ProductId is null)
        {
            return Result.Failure("Google Play returned a subscription with no product");
        }

        var product = _catalog.Find(purchase.ProductId);

        if (product is null || product.Kind != DonationKind.Monthly)
        {
            return Result.Failure($"{purchase.ProductId} is not a monthly contribution");
        }

        // A notification for a subscription nobody registered carries no caller, so the row already
        // on file is the fallback link — that is what it was written for.
        var existingMaybe = await _donationSubscriptionsRepository.GetById(purchaseToken, ct);

        var userIdResult = ResolveUserId(
            purchase.UserId ?? (existingMaybe.HasValue ? existingMaybe.Value.UserId : null),
            expectedUserId);

        if (userIdResult.IsFailure)
        {
            return userIdResult;
        }

        var userId = userIdResult.Value;
        var now = DateTime.UtcNow;

        var subscription = new DonationSubscription
        {
            Id = purchaseToken,
            UserId = userId,
            ProductId = purchase.ProductId,
            AmountMinor = product.NominalAmountMinor,
            Currency = _catalog.NominalCurrency,
            IsActive = purchase.IsActive,
            State = purchase.State,
            ExpiresAt = purchase.ExpiresAt,
            Created = existingMaybe.HasValue ? existingMaybe.Value.Created : now,
            Updated = now,
        };

        EnsureWritten(await _donationSubscriptionsRepository.Upsert(subscription, ct), "donation subscription");

        // A resubscribe or a plan change mints a fresh token pointing back at the one it replaces.
        // Standing the old row down keeps one person's continuous monthly gift from looking like two.
        if (purchase.LinkedPurchaseToken is { } linkedToken && linkedToken != purchaseToken)
        {
            await DeactivateSubscription(linkedToken, now, ct);
        }

        // One row per payment, keyed by the order id, which changes every month. Play only ever
        // names the latest, so a month whose notification was missed is simply never written —
        // acceptable for a ledger whose purpose is "has this person given", and the Play Console
        // remains the complete record.
        if (purchase.IsActive && purchase.LatestOrderId is { } orderId)
        {
            await UpsertDonation(
                id: orderId,
                userId: userId,
                purchaseToken: purchaseToken,
                productId: purchase.ProductId,
                orderId: orderId,
                amountMinor: product.NominalAmountMinor,
                kind: DonationKind.Monthly,
                status: DonationStatus.Succeeded,
                subscriptionId: purchaseToken,
                ct);

            await MarkDonated(userId, hasActiveMonthly: true, ct);
        }
        else
        {
            // Someone can hold more than one, so the flag follows whether any is left rather than
            // this one ending. They stay inside the post-donation cooldown regardless, so this only
            // decides what happens once that runs out.
            var stillActive = await _donationSubscriptionsRepository.HasActiveByUserId(userId, ct);

            await UpdatePromptState(userId, state => state with { HasActiveMonthly = stillActive, Updated = now }, now, ct);
        }

        if (purchase.NeedsAcknowledgement)
        {
            var acknowledgeResult = await _play.AcknowledgeSubscription(purchase.ProductId, purchaseToken, ct);

            if (acknowledgeResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Recorded subscription {purchaseToken} but could not acknowledge it: {acknowledgeResult.Error}");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Marks a subscription no longer paying without asking Play about it again. Used only for the
    /// token a newer subscription says it replaces, where Play has already given the answer.
    /// </summary>
    private async Task DeactivateSubscription(string purchaseToken, DateTime now, CancellationToken ct)
    {
        var existingMaybe = await _donationSubscriptionsRepository.GetById(purchaseToken, ct);

        if (existingMaybe.HasNoValue || !existingMaybe.Value.IsActive)
        {
            return;
        }

        var superseded = existingMaybe.Value with
        {
            IsActive = false,
            State = "SUBSCRIPTION_STATE_REPLACED",
            Updated = now,
        };

        EnsureWritten(await _donationSubscriptionsRepository.Upsert(superseded, ct), "donation subscription");
    }

    /// <summary>
    /// Decides who a purchase belongs to.
    ///
    /// Play's own account id wins, because it is what the app set when it launched the purchase and
    /// came back through Google rather than through the caller. A caller claiming a purchase Play
    /// attributes to someone else is refused outright: that is exactly the shape of stealing
    /// somebody's gift by replaying their token.
    /// </summary>
    private static Result<string> ResolveUserId(string? playUserId, string? expectedUserId)
    {
        if (playUserId is null)
        {
            return expectedUserId is not null
                ? expectedUserId
                : Result.Failure<string>("Purchase has no account attached");
        }

        if (expectedUserId is not null && playUserId != expectedUserId)
        {
            Log.Warning(
                "User {CallerId} tried to register a purchase Google Play attributes to {OwnerId}",
                expectedUserId,
                playUserId);

            return Result.Failure<string>("Purchase belongs to another account");
        }

        return playUserId;
    }

    private async Task UpsertDonation(
        string id,
        string userId,
        string purchaseToken,
        string productId,
        string? orderId,
        long amountMinor,
        DonationKind kind,
        DonationStatus status,
        string? subscriptionId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var existingMaybe = await _donationsRepository.GetById(id, ct);

        var donation = new Donation
        {
            Id = id,
            UserId = userId,
            PurchaseToken = purchaseToken,
            ProductId = productId,
            OrderId = orderId,
            AmountMinor = amountMinor,
            Currency = _catalog.NominalCurrency,
            Kind = kind,
            Status = status,
            SubscriptionId = subscriptionId,
            // Preserved on a replay so the ledger keeps saying when the gift first arrived.
            Created = existingMaybe.HasValue ? existingMaybe.Value.Created : now,
            Updated = now,
        };

        EnsureWritten(await _donationsRepository.Upsert(donation, ct), "donation");
    }

    private async Task MarkDonated(string userId, bool hasActiveMonthly, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        await UpdatePromptState(
            userId,
            state => state with
            {
                LastDonatedAt = now,
                // Never turned off here. Only a subscription ending clears it, and a one-off from
                // someone who also gives monthly must not look like they stopped.
                HasActiveMonthly = state.HasActiveMonthly || hasActiveMonthly,
                Updated = now,
            },
            now,
            ct);
    }

    private async Task UpdatePromptState(
        string userId,
        Func<DonationPromptState, DonationPromptState> update,
        DateTime now,
        CancellationToken ct)
    {
        var stateMaybe = await _donationPromptStatesRepository.GetById(userId, ct);

        var state = stateMaybe.HasValue
            ? stateMaybe.Value
            : DonationPromptState.CreateEmpty(userId, now);

        EnsureWritten(await _donationPromptStatesRepository.Upsert(update(state), ct), "donation prompt state");
    }

    /// <summary>
    /// Turns a write that did not land into an exception, so the caller answers 500 and the sender
    /// tries again. Swallowing it would leave a payment Play has already taken with no record on
    /// this side, and nothing with any reason to retry.
    /// </summary>
    private static void EnsureWritten(Result result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to write {what} while recording a donation: {result.Error}");
        }
    }
}
