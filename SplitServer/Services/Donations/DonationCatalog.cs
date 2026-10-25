using Microsoft.Extensions.Options;
using SplitServer.Configuration;
using SplitServer.Models;

namespace SplitServer.Services.Donations;

/// <summary>
/// The list of product ids this server will accept, and what each one means.
///
/// Play will happily verify any purchase made against this app, including one for a product that
/// has nothing to do with donations. Looking the id up here first is what keeps the donation ledger
/// to donations, and is why an unknown id is refused rather than recorded with a guessed amount.
///
/// Two kinds of id are accepted: the configured tiers, and the "Other amount" family, which is a
/// pattern rather than a list (<c>support_amount_1750</c>, <c>support_monthly_1750</c> for 17.50)
/// because there is one product per allowed amount, in steps of
/// <see cref="DonationsSettings.CustomAmountStepMinor"/> up to
/// <see cref="DonationsSettings.CustomAmountMax"/>.
/// </summary>
public class DonationCatalog
{
    public const string CustomOneTimePrefix = "support_amount_";
    public const string CustomMonthlyPrefix = "support_monthly_";
    public const string CustomMonthlyBasePlanId = "monthly";

    private readonly Dictionary<string, DonationProduct> _byProductId;

    public DonationCatalog(IOptions<DonationsSettings> settings)
    {
        Products = settings.Value.ResolveProducts();
        NominalCurrency = settings.Value.NominalCurrency;
        CustomAmountMaxMinor = Math.Max(0, settings.Value.CustomAmountMax) * 100L;
        // Monthly typed amounts exist only alongside one-off ones.
        CustomMonthlyAmountMaxMinor = CustomAmountMaxMinor > 0
            ? Math.Max(0, settings.Value.CustomMonthlyAmountMax) * 100L
            : 0;
        CustomAmountStepMinor = Math.Max(1, settings.Value.CustomAmountStepMinor);

        _byProductId = Products.ToDictionary(x => x.ProductId, StringComparer.Ordinal);
    }

    public DonationProduct[] Products { get; }

    public string NominalCurrency { get; }

    /// <summary>Largest typed one-off amount, in minor units. Zero when "Other amount" is off.</summary>
    public long CustomAmountMaxMinor { get; }

    /// <summary>Largest typed monthly amount, in minor units. Zero when monthly is not offered.</summary>
    public long CustomMonthlyAmountMaxMinor { get; }

    /// <summary>Gap between typed amounts, and the smallest one, in minor units.</summary>
    public int CustomAmountStepMinor { get; }

    public DonationProduct? Find(string productId) =>
        _byProductId.GetValueOrDefault(productId) ?? FindCustom(productId);

    private DonationProduct? FindCustom(string productId)
    {
        DonationKind kind;
        string prefix;

        if (productId.StartsWith(CustomOneTimePrefix, StringComparison.Ordinal))
        {
            kind = DonationKind.OneTime;
            prefix = CustomOneTimePrefix;
        }
        else if (productId.StartsWith(CustomMonthlyPrefix, StringComparison.Ordinal))
        {
            kind = DonationKind.Monthly;
            prefix = CustomMonthlyPrefix;
        }
        else
        {
            return null;
        }

        var digits = productId[prefix.Length..];

        // Exactly the id the client builds: plain digits, no sign, no leading zero, on a step and
        // within range. Anything looser would let two ids name the same amount.
        if (digits.Length == 0 ||
            digits[0] == '0' ||
            !digits.All(char.IsAsciiDigit) ||
            !long.TryParse(digits, out var amountMinor) ||
            amountMinor > (kind == DonationKind.Monthly ? CustomMonthlyAmountMaxMinor : CustomAmountMaxMinor) ||
            amountMinor % CustomAmountStepMinor != 0)
        {
            return null;
        }

        return new DonationProduct
        {
            ProductId = productId,
            Kind = kind,
            // Each product costs its amount in the buyer's own currency, so that amount is the
            // closest the ledger's yardstick can get, the same way the fixed tiers are recorded.
            NominalAmountMinor = amountMinor,
            BasePlanId = kind == DonationKind.Monthly ? CustomMonthlyBasePlanId : null,
        };
    }
}
