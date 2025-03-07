﻿using System.Globalization;
using CSharpFunctionalExtensions;
using NMoneys;

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
            return Result.Failure($"Username must be between {UsernameMinLength} and {UsernameMaxLength} characters");
        }

        if (username.Any(x => !UsernameAllowedChars.Contains(x)))
        {
            return Result.Failure("Username can only contain English letters, numbers, underscores and periods");
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
            return Result.Failure("Username cannot contain subsequent special characters");
        }

        return Result.Success();
    }

    public Result ValidateAmount(decimal amount, string currency)
    {
        if (currency.Any(char.IsLower))
        {
            return Result.Failure("Currency code must be all upper case");
        }

        if (amount <= 0)
        {
            return Result.Failure("Amount must be greater than 0");
        }

        if (!Currency.TryGet(currency, out var parsedCurrency))
        {
            return Result.Failure("Currency should be a valid ISO 4217 code");
        }

        var validCurrency = parsedCurrency!;

        if (!Money.TryParse(amount.ToString(CultureInfo.InvariantCulture), validCurrency, out _))
        {
            return Result.Failure("Amount must respect provided currency format");
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
}