using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

/// <summary>
/// The few facts about Buqs Plus that belong to the whole database rather than to any account. At
/// present that is one: when Plus went live.
/// </summary>
public interface IPlusStateRepository
{
    Task<Maybe<PlusLaunch>> GetLaunch(CancellationToken ct);

    /// <summary>
    /// Writes the launch document if there is none, and leaves the one that is there untouched if
    /// there is. Success either way; the caller reads back to learn which moment stands.
    /// </summary>
    /// <remarks>
    /// One conditional write rather than a read followed by a write. Two instances booting together
    /// would otherwise both find nothing and both write, and whichever landed second would move the
    /// moment every account's free days are counted from.
    /// </remarks>
    Task<Result> InsertLaunchIfAbsent(PlusLaunch launch, CancellationToken ct);
}
