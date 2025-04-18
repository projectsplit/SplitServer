using Serilog;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace SplitServer.HttpClientHandlers;

public class HttpClientLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        var propertyEnrichers = new ILogEventEnricher[]
        {
            new PropertyEnricher("RequestUri", request.RequestUri?.ToString() ?? string.Empty),
            new PropertyEnricher("RequestHeaders", string.Join(" \n", request.Headers.Select(h => $"{h.Key}: {h.Value}"))),
            new PropertyEnricher("RequestBody", request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : string.Empty),
            new PropertyEnricher("ResponseHeaders", string.Join(" \n", response.Headers.Select(h => $"{h.Key}: {h.Value}"))),
            new PropertyEnricher("ResponseBody", await response.Content.ReadAsStringAsync(ct)),
        };

        Log
            .ForContext(propertyEnrichers)
            .Information(
                "HTTP CLIENT: {Method} {Uri} {StatusCode} {ReasonPhrase}",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode,
                response.ReasonPhrase);

        return response;
    }
}