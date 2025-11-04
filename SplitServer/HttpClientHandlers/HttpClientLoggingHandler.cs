using System.Diagnostics;
using System.Net.Http.Headers;
using Serilog;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace SplitServer.HttpClientHandlers;

public class HttpClientLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await base.SendAsync(request, ct);

        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var requestBody = request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : string.Empty;

            var logEventEnrichers = new ILogEventEnricher[]
            {
                new PropertyEnricher("RequestUri", request.RequestUri?.ToString() ?? string.Empty),
                new PropertyEnricher("RequestHeaders", HeaderString(request.Headers)),
                new PropertyEnricher("RequestBody", requestBody),
                new PropertyEnricher("ResponseHeaders", HeaderString(response.Headers)),
                new PropertyEnricher("ResponseBody", await response.Content.ReadAsStringAsync(ct)),
            };

            Log
                .ForContext(logEventEnrichers)
                .Error(
                    "HTTP CLIENT {Method} {Uri} {StatusCode} {ReasonPhrase} in {Elapsed} ms",
                    request.Method,
                    request.RequestUri,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    stopwatch.ElapsedMilliseconds);
        }
        else
        {
            Log
                .Information(
                    "HTTP CLIENT {Method} {Uri} {StatusCode} {ReasonPhrase} in {Elapsed} ms",
                    request.Method,
                    request.RequestUri,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    stopwatch.ElapsedMilliseconds);
        }

        return response;
    }

    private static string HeaderString(HttpHeaders header)
    {
        return string.Join("\n", header.Select(h => $"{h.Key}: {string.Join(" ", h.Value)}"));
    }
}