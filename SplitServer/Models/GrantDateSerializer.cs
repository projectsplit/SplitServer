using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SplitServer.Models;

/// <summary>
/// Reads "when was this granted" from a field that a person may have filled in by hand.
///
/// The server only ever writes a date here. But the field is also the documented way to grant
/// something to one account from the database tool, and whoever does that will not always pick the
/// date type — <c>true</c> is the obvious thing to type. Read strictly, any of those would throw
/// when the account is loaded, and one hand-edited field would lock its owner out of every request
/// they make. So anything that plainly means "yes" is accepted as a grant.
/// </summary>
public class GrantDateSerializer : SerializerBase<DateTime?>
{
    /// <summary>
    /// What a grant with no usable date reads as. The grant is what matters; this only stands in
    /// for a time nobody wrote down, and is recognisable as that rather than passing for a real one.
    /// </summary>
    public static readonly DateTime UnknownDate = DateTime.UnixEpoch;

    public override DateTime? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;

        switch (reader.GetCurrentBsonType())
        {
            case BsonType.Null:
                reader.ReadNull();

                return null;

            case BsonType.DateTime:
                return DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadDateTime()).UtcDateTime;

            case BsonType.Boolean:
                return reader.ReadBoolean() ? UnknownDate : null;

            case BsonType.String:
                return FromText(reader.ReadString());

            case BsonType.Int32:
                return reader.ReadInt32() != 0 ? UnknownDate : null;

            case BsonType.Int64:
                return reader.ReadInt64() != 0 ? UnknownDate : null;

            case BsonType.Double:
                return reader.ReadDouble() != 0 ? UnknownDate : null;

            default:
                // Something was put there on purpose, even if not something this can read.
                reader.SkipValue();

                return UnknownDate;
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime? value)
    {
        if (value is not { } date)
        {
            context.Writer.WriteNull();

            return;
        }

        // Every date this server writes is UTC; one that arrives without a zone is taken as UTC too
        // rather than shifted by wherever the process happens to be running.
        var utc = date.Kind == DateTimeKind.Local
            ? date.ToUniversalTime()
            : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        context.Writer.WriteDateTime(new DateTimeOffset(utc).ToUnixTimeMilliseconds());
    }

    private static DateTime? FromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.Trim().Equals("false", StringComparison.OrdinalIgnoreCase) ||
            text.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : UnknownDate;
    }
}
