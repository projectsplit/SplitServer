using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Web;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using SplitServer.Configuration;

namespace SplitServer.Services.OpenExchangeRates.Models;

public class OpenExchangeRatesClient
{
    private readonly HttpClient _openExchangeRatesHttpClient;
    private readonly string _appId;

    public OpenExchangeRatesClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenExchangeRatesSettings> options)
    {
        _appId = options.Value.AppId;
        _openExchangeRatesHttpClient = httpClientFactory.CreateClient();
        _openExchangeRatesHttpClient.BaseAddress = new Uri("https://openexchangerates.org/api");
        _openExchangeRatesHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
    }

    public async Task<Result<Dictionary<string, decimal>>> GetHistoricalRates(string baseCurrency, DateOnly date, CancellationToken ct)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["app_id"] = _appId;
        queryParams["base"] = baseCurrency;
        queryParams["symbols"] = string.Join(",", OpenExchangeRatesCurrencies);
        var relativePath = $"/historical/{date.ToString("o")}.json?{queryParams}";

        var httpResponseMessage = await _openExchangeRatesHttpClient.GetAsync(relativePath, ct);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            return Result.Failure<Dictionary<string, decimal>>("Error retrieving currency exchange rates");
        }

        var responseString = await httpResponseMessage.Content.ReadAsStringAsync(ct);
        var response = JsonSerializer.Deserialize<OpenExchangeRatesHistoricalResponse>(responseString)!;

        return response.Rates;
    }

    private static readonly List<string> OpenExchangeRatesCurrencies =
    [
        "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB",
        "BRL", "BSD", "BTC", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF", "CLF", "CLP", "CNH", "CNY", "COP", "CRC", "CUC", "CUP", "CVE",
        "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GGP", "GHS", "GIP", "GMD", "GNF", "GTQ",
        "GYD", "HKD", "HNL", "HRK", "HTG", "HUF", "IDR", "ILS", "IMP", "INR", "IQD", "IRR", "ISK", "JEP", "JMD", "JOD", "JPY", "KES", "KGS",
        "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD", "LSL", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT",
        "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP",
        "PKR", "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLL", "SOS", "SRD", "SSP",
        "STD", "STN", "SVC", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS",
        "VEF", "VES", "VND", "VUV", "WST", "XAF", "XAG", "XAU", "XCD", "XDR", "XOF", "XPD", "XPF", "XPT", "YER", "ZAR", "ZMW", "ZWL"
    ];
}