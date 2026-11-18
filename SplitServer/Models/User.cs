using MongoDB.Bson.Serialization.Attributes;

namespace SplitServer.Models;

[BsonIgnoreExtraElements]
public record User : EntityBase
{
    public required string? Email { get; init; }
    public required bool EmailVerified { get; init; }
    public required string? HashedPassword { get; init; }
    public required string Username { get; init; }
    public required string? GoogleId { get; init; }

    /// <summary>
    /// Set while this person has Buqs Plus, and absent otherwise. Written only by
    /// <see cref="Repositories.IUsersRepository.SetPlus"/>, from what Google Play says about their
    /// subscription. Not required, so that code creating or replacing a user never has to know about
    /// it: a <c>with</c> copy carries it along, and a brand new account simply has none.
    /// </summary>
    [BsonIgnoreIfNull]
    public UserPlus? Plus { get; init; }

    /// <summary>
    /// What this person has given in tips, added up. Absent on someone who never has. Written only
    /// by <see cref="Repositories.IUsersRepository.SetDonations"/>, from the donation ledger.
    /// </summary>
    [BsonIgnoreIfNull]
    public UserDonations? Donations { get; init; }

    /// <summary>
    /// When this account was given Buqs Plus for life, and absent on one that was not. The accounts
    /// that existed before Plus cost anything get it on boot; setting it by hand in the database is
    /// the way to give it to anyone else, and any value that plainly means "yes" is accepted there —
    /// see <see cref="GrantDateSerializer"/>.
    /// </summary>
    [BsonIgnoreIfNull]
    [BsonSerializer(typeof(GrantDateSerializer))]
    public DateTime? PlusForLife { get; init; }

    /// <summary>
    /// How far this account has got through the notices that its free Plus days are ending: absent
    /// or 0 for none yet, 1 once the first has gone out, 2 once there is nothing more to send. Only
    /// ever moved forward, and only by a conditional write, so that no notice can go out twice.
    /// </summary>
    [BsonIgnoreIfNull]
    public int? PlusTrialReminderStage { get; init; }
}
