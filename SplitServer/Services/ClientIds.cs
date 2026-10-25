using CSharpFunctionalExtensions;
using SplitServer.Models;
using SplitServer.Repositories;

namespace SplitServer.Services;

/// <summary>
/// Ids the app picks itself for the expenses and transfers it records. Without a connection the app
/// keeps a new one on the phone and sends it once it can, and it may well send it more than once: a
/// request whose answer was lost looks exactly like one that never arrived. Fixing the id before
/// the first attempt is what lets the server tell a resend from a new record, so the retry cannot
/// store a second copy.
/// </summary>
public static class ClientIds
{
    public const string InvalidIdError = "Id must be a GUID";
    public const string TakenIdError = "Something else already has this id";

    /// <summary>
    /// The id a new record is stored under: the one the app picked when it sent one, written the
    /// way server-made ids are, and a fresh one when it sent none.
    /// </summary>
    public static Result<string> Resolve(string? clientId)
    {
        if (clientId is null)
        {
            return Guid.NewGuid().ToString();
        }

        return Guid.TryParse(clientId, out var guid) && guid != Guid.Empty
            ? guid.ToString()
            : Result.Failure<string>(InvalidIdError);
    }

    /// <summary>The ids a batch is stored under, in the batch's order. See <see cref="Resolve"/>.</summary>
    public static Result<List<string>> ResolveAll(IEnumerable<string?> clientIds)
    {
        var ids = new List<string>();

        foreach (var clientId in clientIds)
        {
            var idResult = Resolve(clientId);

            if (idResult.IsFailure)
            {
                return idResult.ConvertFailure<List<string>>();
            }

            ids.Add(idResult.Value);
        }

        return ids;
    }

    /// <summary>
    /// Whether an earlier send of this record is already stored under the app's id. Anything else
    /// under that id is a clash the app has to hear about, never something to answer as created.
    /// </summary>
    /// <param name="creatorId">The member id for a record in a group, the user id for the others.</param>
    public static async Task<Result<bool>> IsAlreadyStored<TEntity, TExpected>(
        IRepositoryBase<TEntity> repository,
        string id,
        string creatorId,
        CancellationToken ct)
        where TEntity : EntityBase
        where TExpected : TEntity, IHasCreator
    {
        var existingMaybe = await repository.GetById(id, ct);

        if (existingMaybe.HasNoValue)
        {
            return false;
        }

        return IsSameCreation<TEntity, TExpected>(existingMaybe.Value, creatorId)
            ? true
            : Result.Failure<bool>(TakenIdError);
    }

    /// <summary>
    /// Stores the records of a batch that are not stored yet, for a batch that may be arriving for
    /// the second time. A first send that only got part of the way leaves the rest to this one, and
    /// a resend of a batch that went through in full stores nothing.
    /// </summary>
    /// <returns>How many records this call stored, so the caller notifies only for a batch that is new.</returns>
    public static async Task<Result<int>> InsertMissing<TEntity, TExpected>(
        IRepositoryBase<TEntity> repository,
        IList<TExpected> records,
        string creatorId,
        CancellationToken ct)
        where TEntity : EntityBase
        where TExpected : TEntity, IHasCreator
    {
        var storedResult = await StoredIds<TEntity, TExpected>(repository, records, creatorId, ct);

        if (storedResult.IsFailure)
        {
            return storedResult.ConvertFailure<int>();
        }

        var missing = records.Where(x => !storedResult.Value.Contains(x.Id)).Cast<TEntity>().ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        var writeResult = await repository.InsertMany(missing, ct);

        if (writeResult.IsSuccess)
        {
            return missing.Count;
        }

        // Two sends of one batch can both get past the check above. The later one then fails on a
        // duplicate id, and what it was trying to store is there already.
        var recheckResult = await StoredIds<TEntity, TExpected>(repository, records, creatorId, ct);

        return recheckResult.IsSuccess && recheckResult.Value.Count == records.Select(x => x.Id).Distinct().Count()
            ? 0
            : writeResult.ConvertFailure<int>();
    }

    private static async Task<Result<HashSet<string>>> StoredIds<TEntity, TExpected>(
        IRepositoryBase<TEntity> repository,
        IList<TExpected> records,
        string creatorId,
        CancellationToken ct)
        where TEntity : EntityBase
        where TExpected : TEntity, IHasCreator
    {
        var existing = await repository.GetByIds(records.Select(x => x.Id).ToList(), ct);

        return existing.All(x => IsSameCreation<TEntity, TExpected>(x, creatorId))
            ? existing.Select(x => x.Id).ToHashSet()
            : Result.Failure<HashSet<string>>(TakenIdError);
    }

    private static bool IsSameCreation<TEntity, TExpected>(TEntity existing, string creatorId)
        where TEntity : EntityBase
        where TExpected : TEntity, IHasCreator =>
        existing is TExpected expected && expected.CreatorId == creatorId;
}
