using CSharpFunctionalExtensions;
using NMoneys;
using SplitServer.Models;

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace SplitServer.Services;

public class ValidationService
{
    public const int UsernameMinLength = 4;
    public const int UsernameMaxLength = 16;

    public HashSet<char> UsernameAllowedChars { get; } = new("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.");

    private readonly HashSet<char> _usernameForbiddenLeadingChars = new("_.");
    private readonly HashSet<char> _usernameForbiddenTrailingChars = new("_.");
    private readonly List<string> _usernameForbiddenSequences = ["_.", "._", "__", ".."];

    public Result ValidateUsername(string username)
    {
        if (username.Length is < UsernameMinLength or > UsernameMaxLength)
        {
            return Result.Failure($"Username length must be between {UsernameMinLength} and {UsernameMaxLength}");
        }

        if (username.Any(x => !UsernameAllowedChars.Contains(x)))
        {
            return Result.Failure("Username can only contain English characters, numbers, underscores and periods");
        }

        if (_usernameForbiddenLeadingChars.Where(username.StartsWith).Any())
        {
            return Result.Failure("Username cannot start with a special character");
        }

        if (_usernameForbiddenTrailingChars.Where(username.EndsWith).Any())
        {
            return Result.Failure("Username cannot end with a special character");
        }

        if (_usernameForbiddenSequences.Any(username.Contains))
        {
            return Result.Failure("Username cannot contain consecutive special characters");
        }

        return Result.Success();
    }

    public Result ValidateAmount(decimal amount, string currency)
    {
        var currencyResult = ValidateCurrency(currency);

        if (currencyResult.IsFailure)
        {
            return currencyResult;
        }

        var validCurrency = currencyResult.Value;

        if (amount <= 0)
        {
            return Result.Failure("Amount must be greater than 0");
        }

        var maxDecimalDigits = validCurrency.SignificantDecimalDigits;

        var amountWithTrimmedInvalidDecimals =
            Math.Truncate(amount * (decimal)Math.Pow(10, maxDecimalDigits)) /
            (decimal)Math.Pow(10, maxDecimalDigits);

        if (amountWithTrimmedInvalidDecimals != amount)
        {
            return Result.Failure($"{currency} supports up to {maxDecimalDigits} decimal digits");
        }

        return Result.Success();
    }

    public Result<Currency> ValidateCurrency(string currency)
    {
        if (currency.Any(char.IsLower))
        {
            return Result.Failure<Currency>("Currency code must be all upper case");
        }

        if (!Currency.TryGet(currency, out var parsedCurrency))
        {
            return Result.Failure<Currency>("Currency should be a valid ISO 4217 code");
        }

        return parsedCurrency!;
    }

    public Result ValidateExpense(
        Group group,
        List<GroupPayment> payments,
        List<GroupShare> shares,
        decimal amount,
        string currency)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        foreach (var payment in payments)
        {
            var paymentValidationResult = ValidateAmount(payment.Amount, currency);

            if (paymentValidationResult.IsFailure)
            {
                return paymentValidationResult;
            }
        }

        foreach (var share in shares)
        {
            var shareValidationResult = ValidateAmount(share.Amount, currency);

            if (shareValidationResult.IsFailure)
            {
                return shareValidationResult;
            }
        }

        var payers = payments.Select(x => x.MemberId).ToList();
        var participants = shares.Select(x => x.MemberId).ToList();

        if (payers.GroupBy(x => x).Any(g => g.Count() > 1) || participants.GroupBy(x => x).Any(g => g.Count() > 1))
        {
            return Result.Failure("Duplicate members not allowed");
        }

        var members = group.Members.Select(x => x.Id).ToList();
        var guests = group.Guests.Select(x => x.Id).ToList();

        if (payers.Concat(participants).Any(x => !members.Concat(guests).Contains(x)))
        {
            return Result.Failure("Payers and participants must be group members or guests");
        }

        if (shares.Any(x => x.Amount <= 0))
        {
            return Result.Failure("Each share amount must be greater than 0");
        }

        if (payments.Any(x => x.Amount <= 0))
        {
            return Result.Failure("Each payment amount must be greater than 0");
        }

        var totalShareAmount = shares.Sum(x => x.Amount);
        var totalPaymentAmount = payments.Sum(x => x.Amount);

        if (totalShareAmount != amount)
        {
            return Result.Failure("Share amount sum must be equal to expense amount");
        }

        if (totalPaymentAmount != amount)
        {
            return Result.Failure("Payment amount sum must be equal to expense amount");
        }

        return Result.Success();
    }

    public Result ValidateBudget(
        decimal amount,
        string currency,
        string description,
        BudgetScope scope,
        BudgetFrequency frequency,
        DateTime? startDate,
        DateTime? endDate,
        string? commencementDay,
        List<string>? targetGroupIds)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        if (targetGroupIds is { Count: > 0 } && !scope.HasFlag(BudgetScope.Group))
        {
            return Result.Failure("Target groups were provided but the 'Group' scope is not selected.");
        }

        if (string.IsNullOrEmpty(description))
        {
            return Result.Failure("Description is required");
        }

        if (frequency is BudgetFrequency.Weekly or BudgetFrequency.Monthly)
        {
            if (string.IsNullOrEmpty(commencementDay))
            {
                return Result.Failure("Commencement day must be provided for weekly and monthly budgets.");
            }
        }

        if (scope == BudgetScope.None)
        {
            return Result.Failure("Budget must have at least one scope selected.");
        }


        if (frequency == BudgetFrequency.Custom)
        {
            if (startDate == null || endDate == null) return Result.Failure("Custom budgets must have a start and end date.");
            if (startDate >= endDate) return Result.Failure("Start date must be before end date.");
            if (endDate < DateTime.UtcNow.Date) return Result.Failure("End date must be in the future.");
        }

        if (!string.IsNullOrWhiteSpace(commencementDay))
        {
            var value = commencementDay.Trim();

            if (int.TryParse(value, out var day))
            {
                if (day < 1 || day > 31) return Result.Failure("Commencement day must be between 1 and 31 or a weekday name.");
            }
            else
            {
                if (!Enum.TryParse<DayOfWeek>(value, true, out _))
                {
                    return Result.Failure("Commencement day must be between 1 and 31 or a weekday name.");
                }
            }
        }

        return Result.Success();
    }

    public Result ValidateNonGroupExpense(List<Payment> payments, List<Share> shares, decimal amount, string currency)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        foreach (var payment in payments)
        {
            var paymentValidationResult = ValidateAmount(payment.Amount, currency);

            if (paymentValidationResult.IsFailure)
            {
                return paymentValidationResult;
            }
        }

        foreach (var share in shares)
        {
            var shareValidationResult = ValidateAmount(share.Amount, currency);

            if (shareValidationResult.IsFailure)
            {
                return shareValidationResult;
            }
        }

        var payers = payments.Select(x => x.UserId).ToList();
        var participants = shares.Select(x => x.UserId).ToList();

        if (payers.GroupBy(x => x).Any(g => g.Count() > 1) || participants.GroupBy(x => x).Any(g => g.Count() > 1))
        {
            return Result.Failure("Duplicate members not allowed");
        }

        if (payments.Any(x => x.Amount <= 0))
        {
            return Result.Failure("Each payment amount must be greater than 0");
        }

        if (shares.Any(x => x.Amount <= 0))
        {
            return Result.Failure("Each share amount must be greater than 0");
        }

        var totalShareAmount = shares.Sum(x => x.Amount);
        var totalPaymentAmount = payments.Sum(x => x.Amount);

        if (totalShareAmount != amount)
        {
            return Result.Failure("Share amount sum must be equal to expense amount");
        }

        if (totalPaymentAmount != amount)
        {
            return Result.Failure("Payment amount sum must be equal to expense amount");
        }

        return Result.Success();
    }

    public Result ValidatePersonalExpense(decimal amount, string currency)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        return Result.Success();
    }

    public Result ValidateTransfer(Group group, string senderId, string receiverId, decimal amount, string currency)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        var allMemberIds = group.Members.Select(m => m.Id).Concat(group.Guests.Select(g => g.Id)).ToList();

        if (allMemberIds.All(x => x != senderId))
        {
            return Result.Failure("Sender must be a group member");
        }

        if (allMemberIds.All(x => x != receiverId))
        {
            return Result.Failure("Receiver must be a group member");
        }

        if (senderId == receiverId)
        {
            return Result.Failure("Receiver must be different from sender");
        }

        return Result.Success();
    }

    public Result ValidateNonGroupTransfer(
        string senderId,
        string receiverId,
        string userId,
        decimal amount,
        string currency)
    {
        var amountValidationResult = ValidateAmount(amount, currency);

        if (amountValidationResult.IsFailure)
        {
            return amountValidationResult;
        }

        if (senderId != userId && receiverId != userId)
        {
            return Result.Failure($"User {userId} must be part of the non-group transfer");
        }

        if (senderId == receiverId)
        {
            return Result.Failure("Receiver must be different from sender");
        }

        if (senderId == "")
        {
            return Result.Failure("Sender must be provided");
        }

        if (receiverId == "")
        {
            return Result.Failure("Receiver must be provided");
        }

        return Result.Success();
    }
}