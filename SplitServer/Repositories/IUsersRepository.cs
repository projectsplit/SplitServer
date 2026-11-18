using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IUsersRepository : IRepositoryBase<User>
{
    /// <summary>
    /// Returns the account that owns this email, i.e. the one that has verified it.
    /// An unverified email can be held by several accounts at once, so it never identifies an owner.
    /// </summary>
    Task<Maybe<User>> GetVerifiedByEmail(string email, CancellationToken ct);

    Task<Maybe<User>> GetByUsername(string username, CancellationToken ct);

    Task<Maybe<User>> GetByGoogleId(string googleId, CancellationToken ct);

    Task<List<User>> SearchByUsername(string keyword, UserIdScope scope, int skip, int pageSize, CancellationToken ct);

    Task<List<User>> GetLatestUsers(UserIdScope scope, int skip, int pageSize, CancellationToken ct);

    Task<bool> AnyWithUsername(string username, CancellationToken ct);

    /// <summary>
    /// Writes the Plus mark on one user, or takes it off when <paramref name="plus"/> is null, and
    /// touches nothing else on the document.
    /// </summary>
    /// <remarks>
    /// A field-level write on purpose, and the only thing that writes this field: saving a user
    /// through <see cref="IRepositoryBase{TEntity}.Update"/> leaves it alone. A renewal arriving
    /// from Google and the person editing their own account at the same moment therefore cannot
    /// undo one another. It never creates a document: an account that has been deleted stays
    /// deleted, and the write is a success that changed nothing.
    /// </remarks>
    Task<Result> SetPlus(string userId, UserPlus? plus, CancellationToken ct);

    /// <summary>
    /// Writes the summary of what one user has given, or removes it when
    /// <paramref name="donations"/> is null. A field-level write on the same terms as
    /// <see cref="SetPlus"/>.
    /// </summary>
    Task<Result> SetDonations(string userId, UserDonations? donations, CancellationToken ct);

    /// <summary>
    /// Gives one account Buqs Plus for life as of <paramref name="grantedAt"/>, or takes the grant
    /// off when it is null. A field-level write on the same terms as <see cref="SetPlus"/>.
    /// </summary>
    Task<Result> SetPlusForLife(string userId, DateTime? grantedAt, CancellationToken ct);

    /// <summary>
    /// Gives Plus for life to every account created before <paramref name="cutOff"/> that does not
    /// have it yet, and says how many that was. Accounts that already carry a grant are left alone,
    /// whenever and however they got it, so running this again changes nothing.
    /// </summary>
    Task<Result<long>> GrantPlusForLifeToAccountsCreatedBefore(DateTime cutOff, DateTime grantedAt, CancellationToken ct);

    /// <summary>
    /// Accounts that may be owed a notice that their free Plus days are ending: created on or before
    /// <paramref name="createdOnOrBefore"/>, not yet at <paramref name="belowStage"/>, and without
    /// Plus for life. Oldest first, at most <paramref name="limit"/>.
    /// </summary>
    /// <param name="createdOnOrAfter">
    /// When set, older accounts are left out: they have Plus for life by the cut-off rule and are
    /// never owed a notice.
    /// </param>
    /// <remarks>
    /// Narrowed by creation date in the query, because that is what keeps a pass from reading every
    /// account there is: an account too young to be near the end of its free days is never fetched.
    /// Whether one that is fetched is really owed anything is for the caller to decide.
    /// </remarks>
    Task<List<User>> GetTrialReminderCandidates(
        DateTime createdOnOrBefore,
        int belowStage,
        DateTime? createdOnOrAfter,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Moves an account's trial reminder stage up to <paramref name="toStage"/>, and says whether
    /// this call was the one that did it. False means it was already there or beyond, or the account
    /// is gone, and the caller must not send whatever the stage stands for.
    /// </summary>
    /// <remarks>
    /// One conditional write, and before sending rather than after: two passes crossing cannot both
    /// claim the same notice, and a crash in between loses one notice where the other order would
    /// send it twice.
    /// </remarks>
    Task<bool> TryAdvanceTrialReminderStage(string userId, int toStage, CancellationToken ct);

    Task EnsureIndexes(CancellationToken ct);
}