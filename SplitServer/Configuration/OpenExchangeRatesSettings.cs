namespace SplitServer.Configuration;

public class OpenExchangeRatesSettings : ISettings
{
    public required string SectionName { get; init; } = "OpenExchangeRates";
    public required string AppId { get; init; }
}