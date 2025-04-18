using Serilog;

namespace SplitServer.HttpClientHandlers;

public class HttpClientLoggingHandler : DelegatingHandler
{
    private readonly IDiagnosticContext _diagnosticContext;

    public HttpClientLoggingHandler(IDiagnosticContext diagnosticContext)
    {
        _diagnosticContext = diagnosticContext;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        _diagnosticContext.Set("RequestUri", request.RequestUri?.ToString() ?? string.Empty);

        var requestHeaders = string.Join(" \n", request.Headers.Select(h => $"{h.Key}: {h.Value}"));
        _diagnosticContext.Set("RequestHeaders", requestHeaders);

        if (request.Content is not null)
        {
            _diagnosticContext.Set("RequestBody", await request.Content.ReadAsStringAsync(ct));
        }

        var responseHeaders = string.Join(" \n", response.Headers.Select(h => $"{h.Key}: {h.Value}"));
        _diagnosticContext.Set("ResponseHeaders", responseHeaders);

        _diagnosticContext.Set("ResponseBody", await response.Content.ReadAsStringAsync(ct));

        Log.Information(
            "HTTP CLIENT: {Method} {Uri} {StatusCode} {ReasonPhrase}",
            request.Method,
            request.RequestUri,
            (int)response.StatusCode,
            response.ReasonPhrase);

        return response;
    }
}