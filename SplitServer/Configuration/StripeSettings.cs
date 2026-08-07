namespace SplitServer.Configuration;

public class StripeSettings : ISettings
{
    public string SectionName { get; init; } = "Stripe";

    /// <summary>
    /// Turns the whole donation feature off. When false no checkout session is ever created and the
    /// prompt never asks, so an instance with no Stripe account behaves as if the feature does not
    /// exist rather than showing people a button that fails.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Stripe secret key. Belongs in the gitignored appsettings.{Environment}.json alongside the
    /// other secrets, never in appsettings.json, which is committed.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Signing secret of the webhook endpoint, used to prove a callback really came from Stripe.
    /// Same rule as <see cref="SecretKey"/>: environment file only.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
