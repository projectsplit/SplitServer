using System.Globalization;
using NMoneys;

namespace SplitServer.Services.Plus;

/// <summary>
/// The words of the notice that an annual plan is about to renew.
///
/// It says three things and no more: when, how much, and where to stop it. A year is long enough
/// to forget a subscription exists, and the point of the notice is that the charge is never a
/// surprise — so the way out is in the same sentence as the date, not left to be looked for.
/// </summary>
public static class PlusRenewalReminder
{
    public const string Title = "Your Buqs Plus plan renews soon";

    /// <summary>Opens the Plus sheet in the app, which shows the plan and the way to manage it.</summary>
    public const string Url = "/?plus=1";

    /// <summary>
    /// The body, with the renewal date as the subscriber's own calendar has it.
    /// </summary>
    /// <param name="renewsAtUtc">When Play will take the next payment.</param>
    /// <param name="timeZoneId">
    /// The subscriber's time zone. A renewal just after midnight is on a different day depending on
    /// where they are, and the date they are told should be the one they will see it happen on.
    /// Unknown or unreadable falls back to UTC rather than failing the reminder.
    /// </param>
    /// <param name="price">What Play says the renewal will cost, when it says.</param>
    /// <param name="currency">ISO code of <paramref name="price"/>.</param>
    public static string Body(DateTime renewsAtUtc, string? timeZoneId, decimal? price, string? currency)
    {
        var date = PlusDates.ToLocal(renewsAtUtc, timeZoneId).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var money = FormatPrice(price, currency);

        // No figure is better than a guessed one: the amount is left out entirely when Play did not
        // give it, rather than quoting a price from somewhere else that the charge might not match.
        return money is null
            ? $"Your annual plan renews on {date}. You can manage or cancel it in Google Play."
            : $"Your annual plan renews on {date} for {money}. You can manage or cancel it in Google Play.";
    }

    /// <summary>
    /// The price the way its own currency writes it, such as £19.99 or 19,99 €. A code this server
    /// does not know is shown as the bare amount and code rather than dropped, since the figure is
    /// still Play's and still right.
    /// </summary>
    public static string? FormatPrice(decimal? price, string? currency)
    {
        if (price is not { } amount || string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        return Currency.TryGet(currency.ToUpperInvariant(), out var known) && known is not null
            ? new Money(amount, known).ToString()
            : $"{amount.ToString(CultureInfo.InvariantCulture)} {currency.ToUpperInvariant()}";
    }
}
