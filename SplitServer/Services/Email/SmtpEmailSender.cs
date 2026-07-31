using CSharpFunctionalExtensions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Serilog;
using SplitServer.Configuration;

namespace SplitServer.Services.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _emailSettings;

    public SmtpEmailSender(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public async Task<Result> SendAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            var socketOptions = _emailSettings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions, ct);

            if (!string.IsNullOrEmpty(_emailSettings.Username))
            {
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send email to {ToAddress}", toAddress);
            return Result.Failure($"Failed to send email: {ex.Message}");
        }
    }
}
