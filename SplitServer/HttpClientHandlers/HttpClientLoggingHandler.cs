using System.Net.Http.Headers;
using Serilog;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace SplitServer.HttpClientHandlers;

public class HttpClientLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var logEventEnrichers = new ILogEventEnricher[]
            {
                new PropertyEnricher("RequestUri", request.RequestUri?.ToString() ?? string.Empty),
                new PropertyEnricher("RequestHeaders", HeaderString(request.Headers)),
                new PropertyEnricher("RequestBody", request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : string.Empty),
                new PropertyEnricher("ResponseHeaders", HeaderString(response.Headers)),
                new PropertyEnricher("ResponseBody", await response.Content.ReadAsStringAsync(ct)),
            };

            Log
                .ForContext(logEventEnrichers)
                .Error(
                    "HTTP CLIENT {Method} {Uri} {StatusCode} {ReasonPhrase}",
                    request.Method,
                    request.RequestUri,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
        }
        else
        {
            Log.Information(
                "HTTP CLIENT {Method} {Uri} {StatusCode} {ReasonPhrase}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode,
                response.ReasonPhrase);
        }

        return response;
    }

    private static string HeaderString(HttpRequestHeaders header)
    {
        return string.Join("\n", header.Select(h => $"{h.Key}: {string.Join(" ", h.Value)}"));
    }

    private static string HeaderString(HttpResponseHeaders header)
    {
        return string.Join("\n", header.Select(h => $"{h.Key}: {string.Join(" ", h.Value)}"));
    }
}