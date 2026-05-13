using CSharpFunctionalExtensions;
using Serilog;

namespace SplitServer.Services.Email;

public class NullEmailSender : IEmailSender
{
    public Task<Result> SendAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        Log.Information("[NullEmailSender] To: {To} | Subject: {Subject}\n{Body}", toAddress, subject, body);

        return Task.FromResult(Result.Success());
    }
}
