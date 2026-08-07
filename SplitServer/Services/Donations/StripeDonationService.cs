using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Models;
using Stripe;
using Stripe.Checkout;

namespace SplitServer.Services.Donations;

/// <summary>
/// The only place that talks to Stripe. Card details never reach this server: a donation is a
/// redirect to a Checkout page Stripe hosts, and the answer comes back as a signed webhook, which
/// keeps the whole feature outside PCI scope.
/// </summary>
public class StripeDonationService
{
    private readonly StripeSettings _stripeSettings;
    private readonly DonationsSettings _donationsSettings;
    private readonly string _clientUrl;
    private readonly IStripeClient? _stripeClient;

    public StripeDonationService(
        IOptions<StripeSettings> stripeSettings,
        IOptions<DonationsSettings> donationsSettings,
        IOptions<AuthSettings> authSettings)
    {
        _stripeSettings = stripeSettings.Value;
        _donationsSettings = donationsSettings.Value;
        _clientUrl = authSettings.Value.ClientUrl.TrimEnd('/');

        // Built once and reused: StripeClient owns an HttpClient, and a new one per request is the
        // socket-exhaustion mistake. Left null when unconfigured so a missing key is a quiet
        // no-feature rather than an exception on the first donation.
        _stripeClient = IsConfigured ? new StripeClient(_stripeSettings.SecretKey) : null;
    }

    /// <summary>
    /// Whether donations can actually be taken. Everything user-facing checks this first, so an
    /// instance without Stripe credentials never shows a button that could only fail.
    /// </summary>
    public bool IsConfigured =>
        _stripeSettings.Enabled && !string.IsNullOrWhiteSpace(_stripeSettings.SecretKey);

    public bool CanVerifyWebhooks => !string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret);

    public async Task<Result<string>> CreateCheckoutSession(
        string userId,
        long amountMinor,
        DonationKind kind,
        CancellationToken ct)
    {
        if (_stripeClient is null)
        {
            return Result.Failure<string>("Donations are not available");
        }

        var isMonthly = kind == DonationKind.Monthly;

        var options = new SessionCreateOptions
        {
            Mode = isMonthly ? "subscription" : "payment",
            SuccessUrl = _clientUrl + _donationsSettings.SuccessPath,
            CancelUrl = _clientUrl + _donationsSettings.CancelPath,

            // Two ways home from the webhook. ClientReferenceId is what Stripe echoes back on the
            // session; metadata is what survives onto the subscription and its later invoices.
            ClientReferenceId = userId,
            Metadata = BuildMetadata(userId, kind),

            // Managed Payments is on by default for newer accounts. It makes Stripe the merchant of
            // record and has it work out and remit tax, which means it insists every line item
            // carries a product tax code. That model is built for selling something; this is a
            // voluntary contribution with nothing given in return, so there is no honest tax
            // category to file it under, and picking one would invite Stripe to add sales tax or
            // VAT on top of a gift. Turned off per session rather than on the account, so the
            // integration behaves the same wherever it is deployed.
            //
            // This only decides how Stripe treats the payment. Whether the money is taxable income
            // is a separate question and not one this setting answers.
            ManagedPayments = new SessionManagedPaymentsOptions { Enabled = false },

            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _donationsSettings.Currency,
                        UnitAmount = amountMinor,
                        Recurring = isMonthly
                            ? new SessionLineItemPriceDataRecurringOptions { Interval = "month" }
                            : null,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = isMonthly ? "Monthly contribution to Buqs" : "Contribution to Buqs",
                            Description = "Voluntary contribution towards running costs. Buqs is free to use.",
                        },
                    },
                },
            ],
        };

        if (isMonthly)
        {
            options.SubscriptionData = new SessionSubscriptionDataOptions { Metadata = BuildMetadata(userId, kind) };
        }
        else
        {
            // Reads "Donate" on the pay button instead of "Pay". Subscription mode rejects it.
            options.SubmitType = "donate";
            options.PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = BuildMetadata(userId, kind) };
        }

        try
        {
            var session = await new SessionService(_stripeClient).CreateAsync(options, cancellationToken: ct);

            return session.Url;
        }
        catch (StripeException ex)
        {
            // The message can name the account or the key, so it is logged and not returned.
            Log.Error(ex, "Stripe rejected a checkout session for user {UserId}", userId);

            return Result.Failure<string>("Could not start the payment. Please try again later.");
        }
    }

    /// <summary>
    /// Turns a raw webhook body into an event, refusing anything not signed with our webhook secret.
    /// Nothing downstream reads the body directly: without this check a stranger who knows the URL
    /// could post an invented "payment succeeded" and get credited.
    /// </summary>
    public Result<Event> ConstructEvent(string payload, string? signatureHeader)
    {
        if (!CanVerifyWebhooks)
        {
            return Result.Failure<Event>("Webhook secret is not configured");
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure<Event>("Missing Stripe signature header");
        }

        try
        {
            // throwOnApiVersionMismatch is off deliberately. Stripe sends events at the account's
            // own API version, which is pinned per account and drifts from whatever this SDK was
            // built against; every field read below has been stable across that drift, and the
            // alternative is every webhook failing the day the SDK is upgraded.
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _stripeSettings.WebhookSecret,
                throwOnApiVersionMismatch: false);

            return stripeEvent;
        }
        catch (StripeException ex)
        {
            return Result.Failure<Event>($"Invalid Stripe signature: {ex.Message}");
        }
    }

    private static Dictionary<string, string> BuildMetadata(string userId, DonationKind kind) =>
        new()
        {
            ["userId"] = userId,
            ["kind"] = kind.ToString(),
        };
}
