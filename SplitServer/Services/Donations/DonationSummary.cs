using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services.Donations;

/// <summary>
/// Adds a person's gifts up into the summary kept on their user document.
///
/// Pure, and always from the whole ledger: the answer for someone is a function of their donation
/// rows and nothing else. That is what makes it safe to run after every write, on every boot and
/// twice in a row — there is no running total to drift, only a figure to recompute.
/// </summary>
public static class DonationSummary
{
    /// <summary>
    /// The summary for one person's rows, or null when none of them is a payment that arrived.
    /// </summary>
    /// <param name="donations">Every row for the person, whatever its status.</param>
    /// <param name="nominalCurrency">The currency the tiers' nominal amounts are expressed in.</param>
    /// <remarks>
    /// Only succeeded rows count. A pending one is money that has not arrived, and a failed one is
    /// either a payment that never went through or one that was refunded — which is how a refund
    /// comes back out of the total without anything having to subtract it.
    /// </remarks>
    public static UserDonations? From(IEnumerable<Donation> donations, string nominalCurrency)
    {
        var received = donations.Where(x => x.Status == DonationStatus.Succeeded).ToList();

        if (received.Count == 0)
        {
            return null;
        }

        return new UserDonations
        {
            Count = received.Count,
            TotalMinor = received.Sum(x => x.AmountMinor),
            Currency = nominalCurrency,
            // Per currency, because Play charges each buyer in their own and a person can move
            // country. Rows whose order could not be read are simply absent from this, which is why
            // it is kept apart from the nominal total rather than replacing it.
            Charged = received
                .Where(x => x.ChargedAmount is not null && !string.IsNullOrWhiteSpace(x.ChargedCurrency))
                .GroupBy(x => x.ChargedCurrency!.ToUpperInvariant(), StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new ChargedTotal { Currency = x.Key, Amount = x.Sum(y => y.ChargedAmount!.Value) })
                .ToList(),
            FirstDonatedAt = received.Min(x => x.Created),
            LastDonatedAt = received.Max(x => x.Created),
        };
    }
}

/// <summary>
/// Keeps <see cref="User.Donations"/> in step with the donation ledger.
/// </summary>
/// <remarks>
/// Nothing here may fail the thing that called it. The ledger is the record and is already written
/// by the time this runs; the summary is a convenience copy of it. A summary that could not be
/// written is logged and left for the next gift or the next boot to put right, rather than turned
/// into an error on a payment that in fact went through.
/// </remarks>
public class DonationSummaries
{
    private readonly IDonationsRepository _donationsRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly DonationCatalog _catalog;

    public DonationSummaries(
        IDonationsRepository donationsRepository,
        IUsersRepository usersRepository,
        DonationCatalog catalog)
    {
        _donationsRepository = donationsRepository;
        _usersRepository = usersRepository;
        _catalog = catalog;
    }

    /// <summary>Recomputes one person's summary from their rows and writes it. Never throws.</summary>
    public async Task Refresh(string userId, CancellationToken ct)
    {
        try
        {
            var donations = await _donationsRepository.GetByUserId(userId, ct);

            var result = await _usersRepository.SetDonations(
                userId,
                DonationSummary.From(donations, _catalog.NominalCurrency),
                ct);

            if (result.IsFailure)
            {
                Log.Warning("Could not write the donation summary for user {UserId}: {Error}", userId, result.Error);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Warning(ex, "Could not refresh the donation summary for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Writes the summary for everyone the ledger says has given. Run on boot so that gifts recorded
    /// before the summary existed get one, and so that a summary missed by a failed write is never
    /// wrong for longer than until the next restart. Returns how many people were covered.
    /// </summary>
    public async Task<int> RefreshAll(CancellationToken ct)
    {
        var userIds = await _donationsRepository.GetDonorUserIds(ct);

        foreach (var userId in userIds)
        {
            await Refresh(userId, ct);
        }

        return userIds.Count;
    }
}
