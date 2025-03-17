namespace SplitServer.Configuration;

public class OpenTelemetrySettings : ISettings
{
    public required string SectionName { get; init; } = "OpenTelemetry";
    public required bool Enabled { get; init; }
    public required string Endpoint { get; init; }
}