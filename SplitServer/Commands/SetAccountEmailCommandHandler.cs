using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;
using SplitServer.Services.Email;

namespace SplitServer.Commands;

public class SetAccountEmailCommandHandler : IRequestHandler<SetAccountEmailCommand, Result>
{
    private const int VerificationCodeTtlMinutes = 10;

    private readonly IUsersRepository _usersRepository;
    private readonly IEmailVerificationCodesRepository _codesRepository;
    private readonly IEmailSender _emailSender;
    private readonly EmailTokenService _tokenService;
    private readonly ValidationService _validationService;

    public SetAccountEmailCommandHandler(
        IUsersRepository usersRepository,
        IEmailVerificationCodesRepository codesRepository,
        IEmailSender emailSender,
        EmailTokenService tokenService,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _codesRepository = codesRepository;
        _emailSender = emailSender;
        _tokenService = tokenService;
        _validationService = validationService;
    }

    public async Task<Result> Handle(SetAccountEmailCommand command, CancellationToken ct)
    {
        var emailValidationResult = _validationService.ValidateEmail(command.Email);

        if (emailValidationResult.IsFailure)
        {
            return emailValidationResult;
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        var user = userMaybe.Value;

        var isSameAsCurrent = user.Email is not null &&
                              string.Equals(user.Email, command.Email, StringComparison.InvariantCultureIgnoreCase);

        if (!isSameAsCurrent)
        {
            var emailOwnerMaybe = await _usersRepository.GetByEmail(command.Email, ct);

            if (emailOwnerMaybe.HasValue && emailOwnerMaybe.Value.Id != user.Id)
            {
                return Result.Failure("An account with this email already exists");
            }
        }

        var now = DateTime.UtcNow;

        var updateResult = await _usersRepository.Update(
            user with
            {
                Email = command.Email,
                EmailVerified = false,
                Updated = now,
            },
            ct);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var invalidateResult = await _codesRepository.InvalidateActiveCodes(
            user.Id,
            EmailVerificationCodePurpose.VerifyEmail,
            ct);

        if (invalidateResult.IsFailure)
        {
            Log.Warning("Failed to invalidate existing verification codes for user {UserId}", user.Id);
        }

        var plaintextCode = _tokenService.GenerateVerificationCode();
        var codeHash = _tokenService.Hash(plaintextCode);

        var verificationCode = new EmailVerificationCode
        {
            Id = Guid.NewGuid().ToString(),
            Created = now,
            Updated = now,
            UserId = user.Id,
            CodeHash = codeHash,
            Purpose = EmailVerificationCodePurpose.VerifyEmail,
            ExpiresAt = now.AddMinutes(VerificationCodeTtlMinutes),
            ConsumedAt = null,
        };

        var insertResult = await _codesRepository.Insert(verificationCode, ct);

        if (insertResult.IsFailure)
        {
            return insertResult;
        }

        var body =
            $"Hi {user.Username},\n\n" +
            $"Your verification code is: {plaintextCode}\n" +
            $"This code expires in {VerificationCodeTtlMinutes} minutes.\n\n" +
            "If you did not request this, you can ignore this email.";

        var sendResult = await _emailSender.SendAsync(command.Email, "Verify your email", body, ct);

        if (sendResult.IsFailure)
        {
            Log.Warning("Failed to send verification email for user {UserId}: {Error}", user.Id, sendResult.Error);
        }

        return Result.Success();
    }
}
