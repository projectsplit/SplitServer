using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Donations;
using Stripe;
using CheckoutSession = Stripe.Checkout.Session;

namespace SplitServer.Commands;

/// <summary>
/// Where a donation actually becomes real. The browser is never believed about a payment — it is
/// told to go to Stripe and comes back with nothing but a redirect, so this callback is the only
/// thing that writes to the ledger.
///
/// Stripe retries a webhook until it gets a 2xx and will happily deliver the same event twice, so
/// every write here has to survive being run again. Each one is keyed by a Stripe object id and
/// sets fields to values rather than incrementing them, which makes a replay overwrite the row it
/// wrote the first time instead of counting a second gift.
///
/// Failing and refusing are different answers. A <c>Result</c> failure here means the body will
/// never be acceptable — a bad signature, or an event whose payload is not what its type says —
/// and becomes a 400 so Stripe stops. A write that did not land throws instead, becoming a 500 that
/// Stripe retries, because that one really can succeed on the next attempt and dropping it would
/// lose a payment that has already been taken.
/// </summary>
public class ProcessStripeWebhookCommandHandler : IRequestHandler<ProcessStripeWebhookCommand, Result>
{
    private const string CheckoutSessionCompleted = "checkout.session.completed";
    private const string CheckoutSessionAsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
    private const string CheckoutSessionAsyncPaymentFailed = "checkout.session.async_payment_failed";
    private const string InvoicePaid = "invoice.paid";
    private const string CustomerSubscriptionDeleted = "customer.subscription.deleted";

    /// <summary>
    /// Stripe's billing_reason for a renewal. The first invoice of a subscription says
    /// "subscription_create" instead, and is skipped here because the checkout session that started
    /// it already recorded that month.
    /// </summary>
    private const string SubscriptionCycle = "subscription_cycle";

    private readonly StripeDonationService _stripeDonationService;
    private readonly IDonationsRepository _donationsRepository;
    private readonly IDonationSubscriptionsRepository _donationSubscriptionsRepository;
    private readonly IDonationPromptStatesRepository _donationPromptStatesRepository;

    public ProcessStripeWebhookCommandHandler(
        StripeDonationService stripeDonationService,
        IDonationsRepository donationsRepository,
        IDonationSubscriptionsRepository donationSubscriptionsRepository,
        IDonationPromptStatesRepository donationPromptStatesRepository)
    {
        _stripeDonationService = stripeDonationService;
        _donationsRepository = donationsRepository;
        _donationSubscriptionsRepository = donationSubscriptionsRepository;
        _donationPromptStatesRepository = donationPromptStatesRepository;
    }

    public async Task<Result> Handle(ProcessStripeWebhookCommand command, CancellationToken ct)
    {
        var eventResult = _stripeDonationService.ConstructEvent(command.Payload, command.SignatureHeader);

        if (eventResult.IsFailure)
        {
            return eventResult;
        }

        var stripeEvent = eventResult.Value;

        return stripeEvent.Type switch
        {
            CheckoutSessionCompleted or CheckoutSessionAsyncPaymentSucceeded =>
                await HandleCheckoutCompleted(stripeEvent, ct),

            CheckoutSessionAsyncPaymentFailed =>
                await HandleCheckoutFailed(stripeEvent, ct),

            InvoicePaid =>
                await HandleInvoicePaid(stripeEvent, ct),

            CustomerSubscriptionDeleted =>
                await HandleSubscriptionDeleted(stripeEvent, ct),

            // Everything else is subscribed to by accident or by a future change. Succeeding on it
            // is what stops Stripe retrying an event nothing here will ever act on.
            _ => Result.Success(),
        };
    }

    private async Task<Result> HandleCheckoutCompleted(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not CheckoutSession session)
        {
            return Result.Failure($"Event {stripeEvent.Id} did not carry a checkout session");
        }

        var userId = ResolveUserId(session);

        if (userId is null)
        {
            // Nothing to attach it to. Logged rather than failed: retrying cannot conjure the id
            // back, and the money is Stripe's record either way.
            Log.Warning("Stripe checkout session {SessionId} completed with no user id attached", session.Id);

            return Result.Success();
        }

        var isMonthly = session.Mode == "subscription";
        var kind = isMonthly ? DonationKind.Monthly : DonationKind.OneTime;

        // Cards settle inside the checkout, but delayed methods leave the session complete and
        // unpaid until a later async event. Recording it as pending keeps the row honest until then.
        var isPaid = session.PaymentStatus is "paid" or "no_payment_required";

        await UpsertDonation(
            id: session.Id,
            userId: userId,
            amountMinor: session.AmountTotal ?? 0,
            currency: session.Currency ?? string.Empty,
            kind: kind,
            status: isPaid ? DonationStatus.Succeeded : DonationStatus.Pending,
            subscriptionId: session.SubscriptionId,
            ct);

        if (isMonthly && session.SubscriptionId is not null)
        {
            await UpsertSubscription(
                session.SubscriptionId,
                userId,
                session.AmountTotal ?? 0,
                session.Currency ?? string.Empty,
                isActive: true,
                ct);
        }

        if (isPaid)
        {
            await MarkDonated(userId, hasActiveMonthly: isMonthly, ct);
        }

        return Result.Success();
    }

    private async Task<Result> HandleCheckoutFailed(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not CheckoutSession session)
        {
            return Result.Failure($"Event {stripeEvent.Id} did not carry a checkout session");
        }

        var existingMaybe = await _donationsRepository.GetById(session.Id, ct);

        if (existingMaybe.HasNoValue)
        {
            return Result.Success();
        }

        var failed = existingMaybe.Value with
        {
            Status = DonationStatus.Failed,
            Updated = DateTime.UtcNow,
        };

        EnsureWritten(await _donationsRepository.Upsert(failed, ct), "donation");

        return Result.Success();
    }

    private async Task<Result> HandleInvoicePaid(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
        {
            return Result.Failure($"Event {stripeEvent.Id} did not carry an invoice");
        }

        // Only renewals. The first month arrived as a checkout session and is already on the ledger.
        if (invoice.BillingReason != SubscriptionCycle)
        {
            return Result.Success();
        }

        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;

        if (subscriptionId is null)
        {
            Log.Warning("Stripe invoice {InvoiceId} was a subscription cycle with no subscription id", invoice.Id);

            return Result.Success();
        }

        // Whose subscription this is was written down at checkout. An invoice months later carries
        // no dependable trace of the user, so this lookup is the link rather than anything on the event.
        var subscriptionMaybe = await _donationSubscriptionsRepository.GetById(subscriptionId, ct);

        if (subscriptionMaybe.HasNoValue)
        {
            Log.Warning("Stripe invoice {InvoiceId} referenced unknown subscription {SubscriptionId}", invoice.Id, subscriptionId);

            return Result.Success();
        }

        var subscription = subscriptionMaybe.Value;

        await UpsertDonation(
            id: invoice.Id,
            userId: subscription.UserId,
            amountMinor: invoice.AmountPaid,
            currency: invoice.Currency ?? subscription.Currency,
            kind: DonationKind.Monthly,
            status: DonationStatus.Succeeded,
            subscriptionId: subscriptionId,
            ct);

        await MarkDonated(subscription.UserId, hasActiveMonthly: true, ct);

        return Result.Success();
    }

    private async Task<Result> HandleSubscriptionDeleted(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
        {
            return Result.Failure($"Event {stripeEvent.Id} did not carry a subscription");
        }

        var existingMaybe = await _donationSubscriptionsRepository.GetById(subscription.Id, ct);

        if (existingMaybe.HasNoValue)
        {
            return Result.Success();
        }

        var existing = existingMaybe.Value;
        var now = DateTime.UtcNow;

        EnsureWritten(
            await _donationSubscriptionsRepository.Upsert(existing with { IsActive = false, Updated = now }, ct),
            "donation subscription");

        // Someone can hold more than one, so the flag follows whether any is left rather than this
        // one ending. They stay inside the post-donation cooldown regardless, so this only decides
        // what happens after that runs out.
        var stillActive = await _donationSubscriptionsRepository.HasActiveByUserId(existing.UserId, ct);

        await UpdatePromptState(
            existing.UserId,
            state => state with { HasActiveMonthly = stillActive, Updated = now },
            now,
            ct);

        return Result.Success();
    }

    private async Task UpsertDonation(
        string id,
        string userId,
        long amountMinor,
        string currency,
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
            AmountMinor = amountMinor,
            Currency = currency,
            Kind = kind,
            Status = status,
            SubscriptionId = subscriptionId,
            // Preserved on a replay so the ledger keeps saying when the gift first arrived.
            Created = existingMaybe.HasValue ? existingMaybe.Value.Created : now,
            Updated = now,
        };

        EnsureWritten(await _donationsRepository.Upsert(donation, ct), "donation");
    }

    private async Task UpsertSubscription(
        string subscriptionId,
        string userId,
        long amountMinor,
        string currency,
        bool isActive,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var existingMaybe = await _donationSubscriptionsRepository.GetById(subscriptionId, ct);

        var subscription = new DonationSubscription
        {
            Id = subscriptionId,
            UserId = userId,
            AmountMinor = amountMinor,
            Currency = currency,
            IsActive = isActive,
            Created = existingMaybe.HasValue ? existingMaybe.Value.Created : now,
            Updated = now,
        };

        EnsureWritten(await _donationSubscriptionsRepository.Upsert(subscription, ct), "donation subscription");
    }

    private async Task MarkDonated(string userId, bool hasActiveMonthly, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        await UpdatePromptState(
            userId,
            state => state with
            {
                LastDonatedAt = now,
                // Never turned off here. Only the subscription ending clears it, and a one-off from
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
    /// Turns a write that did not land into an exception, so the endpoint answers 500 and Stripe
    /// delivers the event again. Swallowing it would leave a payment Stripe has already taken with
    /// no record on this side, and Stripe with no reason to try again.
    /// </summary>
    private static void EnsureWritten(Result result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to write {what} while handling a Stripe webhook: {result.Error}");
        }
    }

    /// <summary>
    /// ClientReferenceId is the primary carrier and metadata the fallback, because the two are set
    /// together at checkout and either can be the one that survives a given event shape.
    /// </summary>
    private static string? ResolveUserId(CheckoutSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ClientReferenceId))
        {
            return session.ClientReferenceId;
        }

        if (session.Metadata is not null &&
            session.Metadata.TryGetValue("userId", out var fromMetadata) &&
            !string.IsNullOrWhiteSpace(fromMetadata))
        {
            return fromMetadata;
        }

        return null;
    }
}
