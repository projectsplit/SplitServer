using System.Text.Json;
using NMoneys;
using Xunit.Abstractions;

namespace SplitServer.Tests;

public class CurrencyTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly JsonSerializerOptions? _serializerOptions;

    public CurrencyTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IndentSize = 2,
            WriteIndented = true
        };
    }

    [Fact]
    private void CreateCurrencyJson()
    {
        var currencies = Enum.GetValues(typeof(CurrencyIsoCode))
            .Cast<CurrencyIsoCode>()
            .Where(c => !c.IsObsolete())
            .OrderBy(c => c.AsCurrency().IsoSymbol)
            .ToDictionary(
                c => c.ToString(),
                c => new
                {
                    SignificantDecimalDigits = Currency.Get(c).SignificantDecimalDigits,
                    Symbol = Currency.Get(c).Symbol,
                    EnglishName = Currency.Get(c).EnglishName,
                }
            );

        var currencyJson = JsonSerializer.Serialize(currencies, _serializerOptions);

        _testOutputHelper.WriteLine(currencyJson);
    }
}