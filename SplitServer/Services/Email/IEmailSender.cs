using CSharpFunctionalExtensions;

namespace SplitServer.Services.Email;

public interface IEmailSender
{
    Task<Result> SendAsync(string toAddress, string subject, string body, CancellationToken ct);
}
