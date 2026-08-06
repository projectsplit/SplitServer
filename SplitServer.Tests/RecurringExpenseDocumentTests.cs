using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SplitServer.Repositories.Implementations.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Tests;

/// <summary>
/// Guards deserialization against documents written by a different build of the app. The schedule
/// replaced an earlier frequency field, and a single leftover document carrying the old one used to
/// fail the entire list query with a FormatException — a 500 that reached the browser disguised as
/// a CORS error, because the exception escaped before the CORS middleware could add its headers.
/// </summary>
public class RecurringExpenseDocumentTests
{
    private static BsonDocument LegacyPersonalTemplate() => new()
    {
        { "_id", "template-1" },
        { "_t", new BsonArray { "RecurringExpenseMongoDbDocument", "personal" } },
        { "Created", DateTime.UtcNow },
        { "Updated", DateTime.UtcNow },
        { "UserId", "user-1" },
        { "Amount", 12.5m },
        { "Currency", "EUR" },
        { "Description", "rent" },
        { "Location", BsonNull.Value },
        { "Labels", new BsonArray() },
        // The field that no longer exists on the document class.
        { "Frequency", 1 },
        { "TimeZoneId", "Europe/Athens" },
        { "AnchorDate", DateTime.UtcNow },
        { "NextOccurrence", DateTime.UtcNow },
        { "IsPaused", false },
        { "LastExpenseId", BsonNull.Value },
        { "LastRunAt", BsonNull.Value },
        { "LastError", BsonNull.Value }
    };

    [Fact]
    public void A_document_from_an_earlier_shape_still_deserializes()
    {
        var document = BsonSerializer.Deserialize<RecurringExpenseMongoDbDocument>(LegacyPersonalTemplate());

        Assert.Equal("template-1", document.Id);
        Assert.IsType<PersonalRecurringExpenseMongoDbDocument>(document);
    }

    [Fact]
    public void A_document_with_no_schedule_maps_to_an_entity_with_none()
    {
        // Rather than throwing or inventing one: the null is what tells the worker to pause the
        // template and the client to offer a repair instead of rendering a schedule.
        var document = BsonSerializer.Deserialize<RecurringExpenseMongoDbDocument>(LegacyPersonalTemplate());

        var entity = new RecurringExpenseMapper().ToEntity(document);

        Assert.Null(entity.Schedule);
        Assert.Equal("rent", entity.Description);
    }

    [Fact]
    public void An_expense_from_an_earlier_shape_still_deserializes()
    {
        var expense = new BsonDocument
        {
            { "_id", "expense-1" },
            { "_t", new BsonArray { "ExpenseMongoDbDocument", "personal" } },
            { "Created", DateTime.UtcNow },
            { "Updated", DateTime.UtcNow },
            { "Occurred", DateTime.UtcNow },
            { "CreatorId", "user-1" },
            { "Amount", 3m },
            { "Description", "coffee" },
            { "Currency", "EUR" },
            { "Location", BsonNull.Value },
            { "Labels", new BsonArray() },
            { "SomeFieldFromAnotherBranch", "whatever" }
        };

        var document = BsonSerializer.Deserialize<ExpenseMongoDbDocument>(expense);

        Assert.Equal("expense-1", document.Id);
    }
}
