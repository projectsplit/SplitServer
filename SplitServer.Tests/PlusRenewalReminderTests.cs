using System.Globalization;
using SplitServer.Services.Plus;

namespace SplitServer.Tests;

/// <summary>
/// The words of the notice that an annual plan is about to renew. It exists so that the charge is
/// never a surprise, which makes a wrong date or a wrong amount worse than no notice at all — so
/// the date, the amount and the fallbacks for when either is unknown are each pinned.
/// </summary>
public class PlusRenewalReminderTests
{
    private static readonly DateTime RenewsAt = new(2027, 12, 12, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Says_when_how_much_and_where_to_stop_it()
    {
        var body = PlusRenewalReminder.Body(RenewsAt, "Europe/London", 19.99m, "GBP");

        Assert.Equal(
            "Your annual plan renews on 12 December 2027 for £19.99. You can manage or cancel it in Google Play.",
            body);
    }

    /// <summary>No figure is better than a guessed one.</summary>
    [Theory]
    [InlineData("GBP")]
    [InlineData(null)]
    public void Leaves_the_amount_out_when_Play_did_not_give_one(string? currency)
    {
        var body = PlusRenewalReminder.Body(RenewsAt, "Europe/London", price: null, currency);

        Assert.Equal(
            "Your annual plan renews on 12 December 2027. You can manage or cancel it in Google Play.",
            body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Leaves_the_amount_out_when_its_currency_is_unknown(string? currency)
    {
        Assert.Null(PlusRenewalReminder.FormatPrice(19.99m, currency));
        Assert.DoesNotContain(" for ", PlusRenewalReminder.Body(RenewsAt, "Europe/London", 19.99m, currency));
    }

    /// <summary>
    /// A renewal just before midnight UTC is on the next day for someone east of Greenwich, and the
    /// date they are told should be the one they will see it happen on.
    /// </summary>
    [Fact]
    public void Gives_the_date_as_the_subscribers_own_calendar_has_it()
    {
        var lateEvening = new DateTime(2027, 12, 12, 23, 30, 0, DateTimeKind.Utc);

        Assert.Contains("13 December 2027", PlusRenewalReminder.Body(lateEvening, "Europe/Athens", null, null));
        Assert.Contains("12 December 2027", PlusRenewalReminder.Body(lateEvening, "America/New_York", null, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nowhere/Land")]
    public void Falls_back_to_the_UTC_date_rather_than_failing_for_an_unreadable_time_zone(string? timeZone)
    {
        var lateEvening = new DateTime(2027, 12, 12, 23, 30, 0, DateTimeKind.Utc);

        Assert.Contains("12 December 2027", PlusRenewalReminder.Body(lateEvening, timeZone, null, null));
    }

    [Theory]
    [InlineData("19.99", "GBP", "£19.99")]
    [InlineData("19.99", "USD", "$19.99")]
    // Lower case is how a code can arrive from elsewhere; it is the same currency.
    [InlineData("19.99", "gbp", "£19.99")]
    // No minor unit, and a thousands separator.
    [InlineData("2950", "JPY", "¥2,950")]
    public void Writes_a_price_the_way_its_own_currency_does(string amount, string currency, string expected)
    {
        Assert.Equal(expected, PlusRenewalReminder.FormatPrice(decimal.Parse(amount, CultureInfo.InvariantCulture), currency));
    }

    [Fact]
    public void Writes_a_euro_price_the_way_the_euro_does()
    {
        var formatted = PlusRenewalReminder.FormatPrice(19.99m, "EUR");

        // The space between the two is whatever the currency's own format uses, so only the parts
        // that carry meaning are pinned.
        Assert.NotNull(formatted);
        Assert.StartsWith("19,99", formatted);
        Assert.EndsWith("€", formatted);
    }

    /// <summary>The figure is still Play's and still right, so it is shown bare rather than dropped.</summary>
    [Fact]
    public void Shows_the_bare_amount_for_a_currency_code_it_does_not_know()
    {
        Assert.Equal("19.99 ZZZ", PlusRenewalReminder.FormatPrice(19.99m, "zzz"));
    }

    [Fact]
    public void Points_the_notification_at_the_Plus_sheet()
    {
        Assert.Equal("/?plus=1", PlusRenewalReminder.Url);
        Assert.Equal("Your Buqs Plus plan renews soon", PlusRenewalReminder.Title);
    }
}
