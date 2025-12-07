using SplitServer.Models;
using SplitServer.Repositories.Implementations.Extensions;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Mappers;

public class ExpenseMapper : IMapper<Expense, ExpenseMongoDbDocument>
{
    public Expense ToEntity(ExpenseMongoDbDocument document)
    {
        return document switch
        {
            GroupExpenseMongoDbDocument d => new GroupExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                Occurred = d.Occurred,
                CreatorId = d.CreatorId,
                Amount = d.Amount,
                Description = d.Description,
                Currency = d.Currency,
                Location = d.Location?.ToLocation(),
                GroupId = d.GroupId,
                Payments = d.Payments,
                Shares = d.Shares,
                Labels = d.Labels,
            },
            NonGroupExpenseMongoDbDocument d => new NonGroupExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                Occurred = d.Occurred,
                CreatorId = d.CreatorId,
                Amount = d.Amount,
                Description = d.Description,
                Currency = d.Currency,
                Location = d.Location?.ToLocation(),
                Payments = d.Payments,
                Shares = d.Shares,
                Labels = d.Labels,
            },
            PersonalExpenseMongoDbDocument d => new PersonalExpense
            {
                Id = d.Id,
                Created = d.Created,
                Updated = d.Updated,
                Occurred = d.Occurred,
                CreatorId = d.CreatorId,
                Amount = d.Amount,
                Description = d.Description,
                Currency = d.Currency,
                Labels = d.Labels,
                Location = d.Location?.ToLocation()
            },
            _ => throw new NotSupportedException($"Mapping for document type '{document.GetType().Name}' is not implemented.")
        };
    }

    public ExpenseMongoDbDocument ToDocument(Expense entity)
    {
        return entity switch
        {
            GroupExpense e => new GroupExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                Occurred = e.Occurred,
                CreatorId = e.CreatorId,
                Amount = e.Amount,
                Description = e.Description,
                Currency = e.Currency,
                Location = e.Location?.ToMongoDbLocation(),
                GroupId = e.GroupId,
                Payments = e.Payments,
                Shares = e.Shares,
                Labels = e.Labels,
            },
            NonGroupExpense e => new NonGroupExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                Occurred = e.Occurred,
                CreatorId = e.CreatorId,
                Amount = e.Amount,
                Description = e.Description,
                Currency = e.Currency,
                Location = e.Location?.ToMongoDbLocation(),
                Payments = e.Payments,
                Shares = e.Shares,
                Labels = e.Labels,
            },
            PersonalExpense e => new PersonalExpenseMongoDbDocument
            {
                Id = e.Id,
                Created = e.Created,
                Updated = e.Updated,
                Occurred = e.Occurred,
                CreatorId = e.CreatorId,
                Amount = e.Amount,
                Description = e.Description,
                Currency = e.Currency,
                Labels = e.Labels,
                Location = e.Location?.ToMongoDbLocation()
            },
            _ => throw new NotSupportedException($"Mapping for entity type '{entity.GetType().Name}' is not implemented.")
        };
    }
}