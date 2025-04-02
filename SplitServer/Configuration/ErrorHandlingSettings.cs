namespace SplitServer.Configuration;

public class ErrorHandlingSettings : ISettings
{
    public required string SectionName { get; init; } = "ErrorHandling";
    public required bool ShowExceptionInResponse { get; init; }
}