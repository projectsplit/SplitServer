using CSharpFunctionalExtensions;
using MongoDB.Driver;
using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class EmailVerificationCodesMongoDbRepository
    : MongoDbRepositoryBase<EmailVerificationCode, EmailVerificationCode>, IEmailVerificationCodesRepository
{
    public EmailVerificationCodesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "EmailVerificationCodes",
            new PassThroughMapper<EmailVerificationCode>())
    {
    }

    public async Task<Maybe<EmailVerificationCode>> GetActiveByCodeHash(
        string codeHash,
        EmailVerificationCodePurpose purpose,
        CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.CodeHash, codeHash),
            FilterBuilder.Eq(x => x.Purpose, purpose),
            FilterBuilder.Eq(x => x.ConsumedAt, null));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Result> InvalidateActiveCodes(
        string userId,
        EmailVerificationCodePurpose purpose,
        CancellationToken ct)
    {
        var filter = FilterBuilder.And(
            FilterBuilder.Eq(x => x.UserId, userId),
            FilterBuilder.Eq(x => x.Purpose, purpose),
            FilterBuilder.Eq(x => x.ConsumedAt, null));

        var update = UpdateBuilder
            .Set(x => x.ConsumedAt, DateTime.UtcNow)
            .Set(x => x.Updated, DateTime.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);

        return result.IsAcknowledged
            ? Result.Success()
            : Result.Failure("Failed to invalidate active codes");
    }
}
