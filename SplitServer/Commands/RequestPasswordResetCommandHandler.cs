using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Result>
{
    private const int PasswordResetTokenTtlMinutes = 60;

    private readonly IUsersRepository _usersRepository;
    private readonly IEmailVerificationCodesRepository _codesRepository;
    private readonly IEmailSender _emailSender;
    private readonly EmailTokenService _tokenService;
    private readonly AuthSettings _authSettings;

    public RequestPasswordResetCommandHandler(
        IUsersRepository usersRepository,
        IEmailVerificationCodesRepository codesRepository,
        IEmailSender emailSender,
        EmailTokenService tokenService,
        IOptions<AuthSettings> authSettings)
    {
        _usersRepository = usersRepository;
        _codesRepository = codesRepository;
        _emailSender = emailSender;
        _tokenService = tokenService;
        _authSettings = authSettings.Value;
    }

    public async Task<Result> Handle(RequestPasswordResetCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Success();
        }

        var userMaybe = await _usersRepository.GetByEmail(command.Email, ct);

        if (userMaybe.HasNoValue || !userMaybe.Value.EmailVerified)
        {
            return Result.Success();
        }

        var user = userMaybe.Value;
        var now = DateTime.UtcNow;

        var invalidateResult = await _codesRepository.InvalidateActiveCodes(
            user.Id,
            EmailVerificationCodePurpose.PasswordReset,
            ct);

        if (invalidateResult.IsFailure)
        {
            Log.Warning("Failed to invalidate existing reset codes for user {UserId}", user.Id);
        }

        var plaintextToken = _tokenService.GeneratePasswordResetToken();
        var tokenHash = _tokenService.Hash(plaintextToken);

        var code = new EmailVerificationCode
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = user.Id,
            CodeHash = tokenHash,
            Purpose = EmailVerificationCodePurpose.PasswordReset,
            ExpiresAt = now.AddMinutes(PasswordResetTokenTtlMinutes),
            ConsumedAt = null,
        };

        var insertResult = await _codesRepository.Insert(code, ct);

        if (insertResult.IsFailure)
        {
            return Result.Success();
        }

        var resetLink = $"{_authSettings.ClientUrl.TrimEnd('/')}/reset-password?token={plaintextToken}";

        var body =
            $"Hi {user.Username},\n\n" +
            "We received a request to reset your password.\n" +
            $"Click the link below to choose a new password. This link expires in {PasswordResetTokenTtlMinutes} minutes.\n\n" +
            $"{resetLink}\n\n" +
            "If you did not request this, you can ignore this email.";

        var sendResult = await _emailSender.SendAsync(user.Email!, "Reset your password", body, ct);

        if (sendResult.IsFailure)
        {
            Log.Warning("Failed to send password reset email for user {UserId}: {Error}", user.Id, sendResult.Error);
        }

        return Result.Success();
    }
}
