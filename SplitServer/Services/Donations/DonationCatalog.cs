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
/// </summary>
public class DonationCatalog
{
    private readonly Dictionary<string, DonationProduct> _byProductId;

    public DonationCatalog(IOptions<DonationsSettings> settings)
    {
        Products = settings.Value.ResolveProducts();
        NominalCurrency = settings.Value.NominalCurrency;

        _byProductId = Products.ToDictionary(x => x.ProductId, StringComparer.Ordinal);
    }

    public DonationProduct[] Products { get; }

    public string NominalCurrency { get; }

    public DonationProduct? Find(string productId) =>
        _byProductId.GetValueOrDefault(productId);
}
