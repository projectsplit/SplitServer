namespace SplitServer.Services;

/// <summary>
/// Works out who an edit actually affected. Group expenses key on member id and non-group ones on
/// user id, so callers project their own shape into a snapshot and diff the two.
/// </summary>
public static class AmountChanges
{
    /// <summary>What one person paid and owed. Both are needed: moving 10 from someone's share to
    /// their payment leaves them net-neutral but is not the same expense for them.</summary>
    public readonly record struct AmountSnapshot(decimal Paid, decimal Owed);

    public static Dictionary<string, AmountSnapshot> Snapshot(
        IEnumerable<(string Key, decimal Amount)> payments,
        IEnumerable<(string Key, decimal Amount)> shares)
    {
        var snapshot = new Dictionary<string, AmountSnapshot>();

        foreach (var (key, amount) in payments)
        {
            var current = snapshot.GetValueOrDefault(key);
            snapshot[key] = current with { Paid = current.Paid + amount };
        }

        foreach (var (key, amount) in shares)
        {
            var current = snapshot.GetValueOrDefault(key);
            snapshot[key] = current with { Owed = current.Owed + amount };
        }

        return snapshot;
    }

    /// <summary>
    /// Keys whose paid or owed amount differs between the two snapshots. Someone added or removed
    /// counts as changed, because their missing side reads as zero. Anything that leaves every
    /// amount alone — renaming the expense, relabelling it, moving its date — yields nothing, which
    /// is what keeps cosmetic edits from notifying anyone.
    /// </summary>
    public static HashSet<string> GetChangedKeys(
        Dictionary<string, AmountSnapshot> before,
        Dictionary<string, AmountSnapshot> after)
    {
        return before.Keys
            .Concat(after.Keys)
            .Where(x => before.GetValueOrDefault(x) != after.GetValueOrDefault(x))
            .ToHashSet();
    }
}
