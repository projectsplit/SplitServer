using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SplitServer.Models;

namespace SplitServer.Tests;

/// <summary>
/// The Plus-for-life field as it is read back from the database. It is the one field on a user that
/// a person is expected to fill in by hand, and whoever does will not always choose the date type.
/// Read strictly, a hand-typed <c>true</c> would throw every time that account was loaded and lock
/// its owner out of the app — so what is pinned here is that nothing a person might plausibly put
/// there can do that, and that anything meaning "yes" is a grant.
/// </summary>
public class GrantDateSerializerTests
{
    private static BsonDocument UserDocument(BsonValue? plusForLife)
    {
        var document = new BsonDocument
        {
            { "_id", "user-1" },
            { "Created", new BsonDateTime(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)) },
            { "Updated", new BsonDateTime(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)) },
            { "Email", BsonNull.Value },
            { "EmailVerified", false },
            { "HashedPassword", BsonNull.Value },
            { "Username", "someone" },
            { "GoogleId", BsonNull.Value },
        };

        if (plusForLife is not null)
        {
            document.Add("PlusForLife", plusForLife);
        }

        return document;
    }

    private static DateTime? Read(BsonValue? plusForLife) =>
        BsonSerializer.Deserialize<User>(UserDocument(plusForLife)).PlusForLife;

    [Fact]
    public void An_account_without_the_field_has_no_grant()
    {
        Assert.Null(Read(null));
        Assert.Null(Read(BsonNull.Value));
    }

    [Fact]
    public void The_date_the_server_wrote_is_read_back_as_it_was()
    {
        var grantedAt = new DateTime(2026, 11, 18, 20, 30, 15, DateTimeKind.Utc);

        var read = Read(new BsonDateTime(grantedAt));

        Assert.Equal(grantedAt, read);
        Assert.Equal(DateTimeKind.Utc, read?.Kind);
    }

    /// <summary>The obvious thing to type when granting it by hand.</summary>
    [Fact]
    public void True_typed_by_hand_is_a_grant()
    {
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(BsonBoolean.True));
    }

    [Fact]
    public void A_date_typed_as_text_is_a_grant_on_that_date()
    {
        Assert.Equal(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc), Read(new BsonString("2026-11-18")));
        Assert.Equal(new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc), Read(new BsonString("2026-11-18T09:30:00Z")));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("true")]
    [InlineData("founder")]
    public void Any_other_text_is_a_grant_with_no_date(string text)
    {
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(new BsonString(text)));
    }

    [Fact]
    public void A_number_or_an_object_is_a_grant_with_no_date()
    {
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(new BsonInt32(1)));
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(new BsonInt64(1)));
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(new BsonDouble(1.0)));
        Assert.Equal(GrantDateSerializer.UnknownDate, Read(new BsonDocument("by", "hand")));
    }

    /// <summary>Whatever plainly means "no" is not a grant, rather than being read as one by accident.</summary>
    [Fact]
    public void What_plainly_means_no_is_not_a_grant()
    {
        Assert.Null(Read(BsonBoolean.False));
        Assert.Null(Read(new BsonString("")));
        Assert.Null(Read(new BsonString("  ")));
        Assert.Null(Read(new BsonString("false")));
        Assert.Null(Read(new BsonString("No")));
        Assert.Null(Read(new BsonInt32(0)));
    }

    /// <summary>
    /// Whatever was put there, the rest of the account still loads. This is the property the whole
    /// serializer exists for.
    /// </summary>
    [Fact]
    public void An_account_with_a_hand_edited_field_still_loads_in_full()
    {
        var user = BsonSerializer.Deserialize<User>(UserDocument(new BsonArray { 1, "two", BsonNull.Value }));

        Assert.Equal("user-1", user.Id);
        Assert.Equal("someone", user.Username);
        Assert.NotNull(user.PlusForLife);
    }

    [Fact]
    public void A_grant_is_written_as_a_date_and_no_grant_is_not_written_at_all()
    {
        var grantedAt = new DateTime(2026, 11, 18, 20, 30, 15, DateTimeKind.Utc);
        var user = BsonSerializer.Deserialize<User>(UserDocument(null));

        var withGrant = (user with { PlusForLife = grantedAt }).ToBsonDocument();
        var without = user.ToBsonDocument();

        Assert.Equal(BsonType.DateTime, withGrant["PlusForLife"].BsonType);
        Assert.Equal(grantedAt, withGrant["PlusForLife"].ToUniversalTime());
        Assert.False(without.Contains("PlusForLife"));
        Assert.False(without.Contains("PlusTrialReminderStage"));
        Assert.False(without.Contains("Plus"));
        Assert.False(without.Contains("Donations"));
    }
}
