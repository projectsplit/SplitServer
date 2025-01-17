using System.Globalization;
using CSharpFunctionalExtensions;
using NMoneys;

namespace SplitServer.Services;

public class ValidationService
{
    public ValidationService()
    {
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