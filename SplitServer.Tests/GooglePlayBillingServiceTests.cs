using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Services.Donations;

namespace SplitServer.Tests;

/// <summary>
/// The parts of the Play integration that can be pinned without Google on the other end: whether
/// donations are considered available, and who is allowed through the notification endpoint.
///
/// The secret check is the only thing standing in front of an endpoint that makes this server call
/// Google's API, and it is the kind of code that keeps working after being quietly weakened — an
/// ordinary string comparison passes every test here except the timing one nobody writes. So the
/// cases that matter are the ones where it must say no.
/// </summary>
public class GooglePlayBillingServiceTests
{
    private static GooglePlayBillingService CreateService(
        bool enabled = true,
        string serviceAccountJson = "not json",
        string notificationSecret = "the-secret")
    {
        var settings = new GooglePlaySettings
        {
            Enabled = enabled,
            PackageName = "com.buqs",
            ServiceAccountJson = serviceAccountJson,
            NotificationSecret = notificationSecret,
        };

        return new GooglePlayBillingService(Options.Create(settings));
    }

    [Fact]
    public void Accepts_the_configured_notification_secret()
    {
        Assert.True(CreateService().IsNotificationSecretValid("the-secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong")]
    // A prefix of the real secret. The comparison must not stop at the first difference in a way
    // that reveals how far a guess got.
    [InlineData("the-")]
    // Longer than the real one, which is where a length-first comparison would be the giveaway.
    [InlineData("the-secret-and-more")]
    [InlineData("The-Secret")]
    public void Rejects_anything_that_is_not_the_configured_secret(string supplied)
    {
        Assert.False(CreateService().IsNotificationSecretValid(supplied));
    }

    [Fact]
    public void Rejects_a_missing_notification_secret()
    {
        Assert.False(CreateService().IsNotificationSecretValid(null));
    }

    /// <summary>
    /// An unset secret must refuse everything rather than let a blank one through. Otherwise the
    /// safest-looking configuration — leaving the field empty — would be the one that opens the
    /// endpoint to anybody.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("anything")]
    public void Rejects_every_secret_when_none_is_configured(string supplied)
    {
        var service = CreateService(notificationSecret: string.Empty);

        Assert.False(service.IsNotificationSecretValid(supplied));
    }

    [Fact]
    public void Is_not_configured_when_donations_are_switched_off()
    {
        Assert.False(CreateService(enabled: false).IsConfigured);
    }

    [Fact]
    public void Is_not_configured_when_there_are_no_credentials()
    {
        Assert.False(CreateService(serviceAccountJson: string.Empty).IsConfigured);
    }

    /// <summary>
    /// Credentials that cannot be read must switch donations off rather than throw. This runs in a
    /// singleton's constructor, so an exception would surface as a failure of whichever unrelated
    /// request happened to resolve it first, and would keep doing so until the process restarted.
    /// </summary>
    [Fact]
    public void Is_not_configured_when_the_credentials_cannot_be_read()
    {
        Assert.False(CreateService(serviceAccountJson: "{ not really json }").IsConfigured);
    }

    /// <summary>
    /// The amount lookup is the one call that is allowed to fail quietly, and with no credentials it
    /// must do exactly that rather than throw into a recording that has otherwise succeeded.
    /// </summary>
    [Fact]
    public async Task Has_no_order_total_to_give_when_Play_is_not_configured()
    {
        var total = await CreateService(enabled: false).TryGetOrderTotal("GPA.1234-5678", CancellationToken.None);

        Assert.Null(total);
    }

    /// <summary>
    /// Google writes money as whole units plus billionths, and leaves either half off the wire when
    /// it is zero. Reading a missing half as a missing amount would lose every round price.
    /// </summary>
    [Theory]
    [InlineData(19L, 990_000_000, "19.99")]
    [InlineData(5L, null, "5")]
    [InlineData(null, 990_000_000, "0.99")]
    [InlineData(2950L, 0, "2950")]
    [InlineData(0L, 0, "0")]
    [InlineData(4L, 490_000_000, "4.49")]
    [InlineData(1L, 1, "1.000000001")]
    public void Reads_an_amount_from_units_and_nanos(long? units, int? nanos, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), PlayMoney.ToAmount(units, nanos));
    }

    [Fact]
    public void Has_no_amount_when_Play_sent_neither_half()
    {
        Assert.Null(PlayMoney.ToAmount(null, null));
    }
}
