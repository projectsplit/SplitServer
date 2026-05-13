using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Repositories;

public interface IEmailVerificationCodesRepository : IRepositoryBase<EmailVerificationCode>
{
    Task<Maybe<EmailVerificationCode>> GetActiveByCodeHash(
        string codeHash,
        EmailVerificationCodePurpose purpose,
        CancellationToken ct);

    Task<Result> InvalidateActiveCodes(
        string userId,
        EmailVerificationCodePurpose purpose,
        CancellationToken ct);
}
