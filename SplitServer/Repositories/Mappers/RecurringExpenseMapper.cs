using SplitServer.Models;
using SplitServer.Repositories.Implementations.Extensions;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Mappers;

public class RecurringExpenseMapper : IMapper<RecurringExpense, RecurringExpenseMongoDbDocument>
{
    public RecurringExpense ToEntity(RecurringExpenseMongoDbDocument document)
    {
        return document switch
        {
            GroupRecurringExpenseMongoDbDocument d => new GroupRecurringExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                UserId = d.UserId,
                Amount = d.Amount,
                Currency = d.Currency,
                Description = d.Description,
                Location = d.Location?.ToLocation(),
                Labels = d.Labels,
                Schedule = d.Schedule,
                TimeZoneId = d.TimeZoneId,
                AnchorDate = d.AnchorDate,
                NextOccurrence = d.NextOccurrence,
                IsPaused = d.IsPaused,
                LastExpenseId = d.LastExpenseId,
                LastRunAt = d.LastRunAt,
                LastError = d.LastError,
                GroupId = d.GroupId,
                Payments = d.Payments,
                Shares = d.Shares
            },
            NonGroupRecurringExpenseMongoDbDocument d => new NonGroupRecurringExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                UserId = d.UserId,
                Amount = d.Amount,
                Currency = d.Currency,
                Description = d.Description,
                Location = d.Location?.ToLocation(),
                Labels = d.Labels,
                Schedule = d.Schedule,
                TimeZoneId = d.TimeZoneId,
                AnchorDate = d.AnchorDate,
                NextOccurrence = d.NextOccurrence,
                IsPaused = d.IsPaused,
                LastExpenseId = d.LastExpenseId,
                LastRunAt = d.LastRunAt,
                LastError = d.LastError,
                Payments = d.Payments,
                Shares = d.Shares
            },
            PersonalRecurringExpenseMongoDbDocument d => new PersonalRecurringExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                UserId = d.UserId,
                Amount = d.Amount,
                Currency = d.Currency,
                Description = d.Description,
                Location = d.Location?.ToLocation(),
                Labels = d.Labels,
                Schedule = d.Schedule,
                TimeZoneId = d.TimeZoneId,
                AnchorDate = d.AnchorDate,
                NextOccurrence = d.NextOccurrence,
                IsPaused = d.IsPaused,
                LastExpenseId = d.LastExpenseId,
                LastRunAt = d.LastRunAt,
                LastError = d.LastError
            },
            _ => throw new NotSupportedException($"Mapping for document type '{document.GetType().Name}' is not implemented.")
        };
    }

    public RecurringExpenseMongoDbDocument ToDocument(RecurringExpense entity)
    {
        return entity switch
        {
            GroupRecurringExpense e => new GroupRecurringExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                UserId = e.UserId,
                Amount = e.Amount,
                Currency = e.Currency,
                Description = e.Description,
                Location = e.Location?.ToMongoDbLocation(),
                Labels = e.Labels,
                Schedule = e.Schedule,
                TimeZoneId = e.TimeZoneId,
                AnchorDate = e.AnchorDate,
                NextOccurrence = e.NextOccurrence,
                IsPaused = e.IsPaused,
                LastExpenseId = e.LastExpenseId,
                LastRunAt = e.LastRunAt,
                LastError = e.LastError,
                GroupId = e.GroupId,
                Payments = e.Payments,
                Shares = e.Shares
            },
            NonGroupRecurringExpense e => new NonGroupRecurringExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                UserId = e.UserId,
                Amount = e.Amount,
                Currency = e.Currency,
                Description = e.Description,
                Location = e.Location?.ToMongoDbLocation(),
                Labels = e.Labels,
                Schedule = e.Schedule,
                TimeZoneId = e.TimeZoneId,
                AnchorDate = e.AnchorDate,
                NextOccurrence = e.NextOccurrence,
                IsPaused = e.IsPaused,
                LastExpenseId = e.LastExpenseId,
                LastRunAt = e.LastRunAt,
                LastError = e.LastError,
                Payments = e.Payments,
                Shares = e.Shares
            },
            PersonalRecurringExpense e => new PersonalRecurringExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                UserId = e.UserId,
                Amount = e.Amount,
                Currency = e.Currency,
                Description = e.Description,
                Location = e.Location?.ToMongoDbLocation(),
                Labels = e.Labels,
                Schedule = e.Schedule,
                TimeZoneId = e.TimeZoneId,
                AnchorDate = e.AnchorDate,
                NextOccurrence = e.NextOccurrence,
                IsPaused = e.IsPaused,
                LastExpenseId = e.LastExpenseId,
                LastRunAt = e.LastRunAt,
                LastError = e.LastError
            },
            _ => throw new NotSupportedException($"Mapping for entity type '{entity.GetType().Name}' is not implemented.")
        };
    }
}
