namespace SplitServer.Configuration;

public class EmailSettings : ISettings
{
    public required string SectionName { get; init; } = "Email";
    public required bool Enabled { get; init; }
    public required string SmtpHost { get; init; }
    public required int SmtpPort { get; init; }
    public required bool UseStartTls { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
}
