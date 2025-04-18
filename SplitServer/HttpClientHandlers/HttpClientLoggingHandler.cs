using Serilog;
using Serilog.Context;

namespace SplitServer.HttpClientHandlers;

public class HttpClientLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        using (LogContext.PushProperty("RequestUri", request.RequestUri?.ToString() ?? string.Empty));

        var requestHeaders = string.Join(" \n", request.Headers.Select(h => $"{h.Key}: {h.Value}"));
        using (LogContext.PushProperty("RequestHeaders", requestHeaders));

        if (request.Content is not null)
        {
            using (LogContext.PushProperty("RequestBody", await request.Content.ReadAsStringAsync(ct)));
        }

        var responseHeaders = string.Join(" \n", response.Headers.Select(h => $"{h.Key}: {h.Value}"));
        using (LogContext.PushProperty("ResponseHeaders", responseHeaders));

        using (LogContext.PushProperty("ResponseBody", await response.Content.ReadAsStringAsync(ct)));

        Log.Information(
            "HTTP CLIENT: {Method} {Uri} {StatusCode} {ReasonPhrase}",
            request.Method,
            request.RequestUri,
            (int)response.StatusCode,
            response.ReasonPhrase);

        return response;
    }
}