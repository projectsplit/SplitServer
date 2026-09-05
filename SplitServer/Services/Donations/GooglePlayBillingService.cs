using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Google;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;

namespace SplitServer.Services.Donations;

/// <summary>
/// The only place that talks to Google Play. No card details ever reach this server: the purchase
/// happens inside Play's own sheet, and all that comes back to the app is a token which means
/// nothing until Google is asked about it here.
///
/// The client is never believed about a purchase. Everything it sends is a claim, and the answer
/// from <c>purchases.products.get</c> or <c>purchases.subscriptionsv2.get</c> is what the ledger is
/// written from.
/// </summary>
public class GooglePlayBillingService
{
    /// <summary>Google's numbering for <c>ProductPurchase.purchaseState</c>.</summary>
    private const int ProductPurchased = 0;

    private const int ProductPending = 2;

    /// <summary>Google's numbering for the acknowledgement and consumption states.</summary>
    private const int NotYetAcknowledged = 0;

    private const int AlreadyConsumed = 1;

    /// <summary>
    /// Subscription states that mean money is currently flowing. <c>CANCELED</c> is deliberately not
    /// among them: the person has turned off renewal, and though they stay entitled until the period
    /// runs out, they have said they are done. The post-donation cooldown is what keeps them from
    /// being asked in the meantime.
    /// </summary>
    private static readonly HashSet<string> ActiveSubscriptionStates =
    [
        "SUBSCRIPTION_STATE_ACTIVE",
        "SUBSCRIPTION_STATE_IN_GRACE_PERIOD",
    ];

    private readonly GooglePlaySettings _settings;
    private readonly AndroidPublisherService? _publisher;

    public GooglePlayBillingService(IOptions<GooglePlaySettings> settings)
    {
        _settings = settings.Value;

        // Built once and reused: the service owns an HttpClient and its credential refreshes its own
        // access token in the background, so one per request would both exhaust sockets and re-do the
        // OAuth handshake every time. Left null when unconfigured so missing credentials are a quiet
        // no-feature rather than an exception on the first donation.
        _publisher = IsEnabled ? CreatePublisher(_settings) : null;
    }

    /// <summary>
    /// Whether donations can actually be taken. Everything user-facing checks this first, so an
    /// instance without usable Play credentials never shows a button that could only fail.
    /// </summary>
    public bool IsConfigured => _publisher is not null;

    private bool IsEnabled =>
        _settings.Enabled &&
        !string.IsNullOrWhiteSpace(_settings.ServiceAccountJson) &&
        !string.IsNullOrWhiteSpace(_settings.PackageName);

    public string PackageName => _settings.PackageName;

    /// <summary>
    /// Whether a notification carrying <paramref name="suppliedSecret"/> may be acted on. Compared in
    /// fixed time so the endpoint cannot be used to guess the secret a character at a time, and
    /// refused outright when no secret is configured rather than letting a blank one match.
    /// </summary>
    public bool IsNotificationSecretValid(string? suppliedSecret)
    {
        if (string.IsNullOrWhiteSpace(_settings.NotificationSecret) || suppliedSecret is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedSecret),
            Encoding.UTF8.GetBytes(_settings.NotificationSecret));
    }

    /// <summary>
    /// Asks Google what really happened for a one-off purchase.
    /// </summary>
    /// <remarks>
    /// A failure here means "could not find out", never "not a real purchase" — the caller must not
    /// turn one into the other, because a Google outage would then read as fraud. A purchase that
    /// Google says is not paid comes back as a success carrying
    /// <see cref="PlayProductPurchase.IsPaid"/> false.
    /// </remarks>
    public async Task<Result<PlayProductPurchase>> GetProductPurchase(
        string productId,
        string purchaseToken,
        CancellationToken ct)
    {
        if (_publisher is null)
        {
            return Result.Failure<PlayProductPurchase>("Donations are not available");
        }

        try
        {
            var purchase = await _publisher.Purchases.Products
                .Get(_settings.PackageName, productId, purchaseToken)
                .ExecuteAsync(ct);

            return new PlayProductPurchase
            {
                OrderId = purchase.OrderId,
                IsPaid = purchase.PurchaseState == ProductPurchased,
                IsPending = purchase.PurchaseState == ProductPending,
                NeedsAcknowledgement = purchase.AcknowledgementState == NotYetAcknowledged,
                IsConsumed = purchase.ConsumptionState == AlreadyConsumed,
                UserId = purchase.ObfuscatedExternalAccountId,
                PurchasedAt = purchase.PurchaseTimeMillis is { } millis
                    ? DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime
                    : null,
            };
        }
        catch (GoogleApiException ex)
        {
            // The message can name the package or the service account, so it is logged and not returned.
            Log.Error(ex, "Google Play rejected a lookup of product {ProductId}", productId);

            return Result.Failure<PlayProductPurchase>("Could not verify the purchase. Please try again later.");
        }
    }

    /// <summary>Asks Google what really happened for a subscription purchase. Same failure rules as above.</summary>
    public async Task<Result<PlaySubscriptionPurchase>> GetSubscriptionPurchase(
        string purchaseToken,
        CancellationToken ct)
    {
        if (_publisher is null)
        {
            return Result.Failure<PlaySubscriptionPurchase>("Donations are not available");
        }

        try
        {
            var purchase = await _publisher.Purchases.Subscriptionsv2
                .Get(_settings.PackageName, purchaseToken)
                .ExecuteAsync(ct);

            // One line item per plan. Donations only ever sell a single plan at a time, so the first
            // is the one, but it is read defensively because the field is a list on the wire.
            var lineItem = purchase.LineItems?.FirstOrDefault();

            return new PlaySubscriptionPurchase
            {
                ProductId = lineItem?.ProductId,
                State = purchase.SubscriptionState ?? string.Empty,
                IsActive = ActiveSubscriptionStates.Contains(purchase.SubscriptionState ?? string.Empty),
                NeedsAcknowledgement = purchase.AcknowledgementState == "ACKNOWLEDGEMENT_STATE_PENDING",
                UserId = purchase.ExternalAccountIdentifiers?.ObfuscatedExternalAccountId,
                ExpiresAt = lineItem?.ExpiryTimeDateTimeOffset?.UtcDateTime,
                LatestOrderId = lineItem?.LatestSuccessfulOrderId,
                // Play issues a fresh token when a subscription is resubscribed or upgraded and
                // points it back at the one it replaces. Following it is what stops one person's
                // single monthly gift turning into a row per renewal chain.
                LinkedPurchaseToken = purchase.LinkedPurchaseToken,
            };
        }
        catch (GoogleApiException ex)
        {
            Log.Error(ex, "Google Play rejected a subscription lookup");

            return Result.Failure<PlaySubscriptionPurchase>("Could not verify the purchase. Please try again later.");
        }
    }

    /// <summary>
    /// Tells Play the gift has been recorded. Unacknowledged purchases are refunded automatically
    /// after three days, so this is what makes the money actually stay, and it runs only once the
    /// ledger write has already landed.
    /// </summary>
    public async Task<Result> AcknowledgeProduct(string productId, string purchaseToken, CancellationToken ct)
    {
        if (_publisher is null)
        {
            return Result.Failure("Donations are not available");
        }

        try
        {
            await _publisher.Purchases.Products
                .Acknowledge(new ProductPurchasesAcknowledgeRequest(), _settings.PackageName, productId, purchaseToken)
                .ExecuteAsync(ct);

            return Result.Success();
        }
        catch (GoogleApiException ex)
        {
            Log.Error(ex, "Failed to acknowledge Google Play product {ProductId}", productId);

            return Result.Failure("Could not acknowledge the purchase");
        }
    }

    /// <summary>
    /// Gives the tier back so the same one can be given again. Consuming also acknowledges, so a
    /// consumed purchase needs no separate acknowledgement.
    /// </summary>
    /// <remarks>
    /// Without this a person who gives £5 owns "the £5 tier" forever and Play refuses to sell it to
    /// them a second time. Donations are the one case where buying the identical thing repeatedly is
    /// the entire point.
    /// </remarks>
    public async Task<Result> ConsumeProduct(string productId, string purchaseToken, CancellationToken ct)
    {
        if (_publisher is null)
        {
            return Result.Failure("Donations are not available");
        }

        try
        {
            await _publisher.Purchases.Products
                .Consume(_settings.PackageName, productId, purchaseToken)
                .ExecuteAsync(ct);

            return Result.Success();
        }
        catch (GoogleApiException ex)
        {
            Log.Error(ex, "Failed to consume Google Play product {ProductId}", productId);

            return Result.Failure("Could not consume the purchase");
        }
    }

    public async Task<Result> AcknowledgeSubscription(string productId, string purchaseToken, CancellationToken ct)
    {
        if (_publisher is null)
        {
            return Result.Failure("Donations are not available");
        }

        try
        {
            await _publisher.Purchases.Subscriptions
                .Acknowledge(
                    new SubscriptionPurchasesAcknowledgeRequest(),
                    _settings.PackageName,
                    productId,
                    purchaseToken)
                .ExecuteAsync(ct);

            return Result.Success();
        }
        catch (GoogleApiException ex)
        {
            Log.Error(ex, "Failed to acknowledge Google Play subscription {ProductId}", productId);

            return Result.Failure("Could not acknowledge the subscription");
        }
    }

    /// <summary>
    /// A malformed service account must not take the whole application down — every unrelated
    /// endpoint still works without it — so a failure here turns donations off and says so, rather
    /// than throwing out of the constructor and failing whichever request happened to resolve this
    /// singleton first.
    /// </summary>
    private static AndroidPublisherService? CreatePublisher(GooglePlaySettings settings)
    {
        try
        {
            var credential = GoogleCredential
                .FromJson(DecodeServiceAccount(settings.ServiceAccountJson))
                .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);

            return new AndroidPublisherService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Buqs",
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Google Play credentials could not be read. Donations are disabled");

            return null;
        }
    }

    /// <summary>
    /// Takes the credentials as configured and returns them as JSON. See
    /// <see cref="GooglePlaySettings.ServiceAccountJson"/> for why both forms exist.
    /// </summary>
    private static string DecodeServiceAccount(string value)
    {
        var trimmed = value.Trim();

        return trimmed.StartsWith('{')
            ? trimmed
            : Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
    }
}

/// <summary>What Google says about a one-off purchase, reduced to the parts the ledger cares about.</summary>
public record PlayProductPurchase
{
    public required string? OrderId { get; init; }

    public required bool IsPaid { get; init; }

    /// <summary>
    /// True for a payment method that settles later, such as cash at a kiosk. The purchase is real
    /// but the money has not arrived, so it is recorded as pending and confirmed by a later
    /// notification rather than credited now.
    /// </summary>
    public required bool IsPending { get; init; }

    public required bool NeedsAcknowledgement { get; init; }

    public required bool IsConsumed { get; init; }

    /// <summary>
    /// The obfuscated account id set when the purchase was launched, which for this app is the user
    /// id. Present on anything the app itself started; absent on a purchase made another way.
    /// </summary>
    public required string? UserId { get; init; }

    public required DateTime? PurchasedAt { get; init; }
}

/// <summary>What Google says about a subscription, reduced to the parts the ledger cares about.</summary>
public record PlaySubscriptionPurchase
{
    public required string? ProductId { get; init; }

    public required string State { get; init; }

    public required bool IsActive { get; init; }

    public required bool NeedsAcknowledgement { get; init; }

    public required string? UserId { get; init; }

    public required DateTime? ExpiresAt { get; init; }

    /// <summary>Play's order id for the most recent successful payment. Changes on every renewal.</summary>
    public required string? LatestOrderId { get; init; }

    public required string? LinkedPurchaseToken { get; init; }
}
