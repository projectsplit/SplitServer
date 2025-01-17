using SplitServer.Models;
using SplitServer.Repositories.Implementations.Extensions;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Mappers;

public class ExpenseMapper : IMapper<Expense, ExpenseMongoDbDocument>
{
    public Expense ToEntity(ExpenseMongoDbDocument document)
    {
        return new Expense
        {
            Id = document.Id,
            IsDeleted = document.IsDeleted,
            Created = document.Created,
            Updated = document.Updated,
            GroupId = document.GroupId,
            CreatorId = document.CreatorId,
            Amount = document.Amount,
            Occured = document.Occured,
            Description = document.Description,
            Currency = document.Currency,
            Payments = document.Payments,
            Shares = document.Shares,
            Labels = document.Labels,
            Location = document.Location?.ToLocation()
        };
    }

    public ExpenseMongoDbDocument ToDocument(Expense entity)
    {
        return new ExpenseMongoDbDocument
        {
            Id = entity.Id,
            IsDeleted = entity.IsDeleted,
            Created = entity.Created,
            Updated = entity.Updated,
            GroupId = entity.GroupId,
            CreatorId = entity.CreatorId,
            Amount = entity.Amount,
            Occured = entity.Occured,
            Description = entity.Description,
            Currency = entity.Currency,
            Payments = entity.Payments,
            Shares = entity.Shares,
            Labels = entity.Labels,
            Location = entity.Location?.ToMongoDbLocation()
        };
    }
}