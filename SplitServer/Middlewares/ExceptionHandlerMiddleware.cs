using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Extensions;
using SplitServer.Services;

namespace SplitServer.Middlewares;

public class ExceptionHandlerMiddleware : IMiddleware
{
    private readonly IDiagnosticContext _diagnosticContext;
    private readonly bool _showExceptionInResponse;

    public ExceptionHandlerMiddleware(
        IDiagnosticContext diagnosticContext,
        IOptions<ErrorHandlingSettings> settings)
    {
        _diagnosticContext = diagnosticContext;
        _showExceptionInResponse = settings.Value.ShowExceptionInResponse;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            context.Request.EnableBuffering();
            await next(context);
            if (context.Response.StatusCode > 299)
            {
                await SetDetailedDiagnosticContextProperties(context);
            }
        }
        catch (BadHttpRequestException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            if (ex.InnerException is JsonException innerException)
            {
                await context.Response.WriteAsJsonAsync(innerException.Message);
            }
            else
            {
                await context.Response.WriteAsJsonAsync(ex.Message);
            }

            _diagnosticContext.SetException(ex);
            await SetDetailedDiagnosticContextProperties(context);
        }
        catch (ResourceLockedException ex)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            _diagnosticContext.SetException(ex);
            await SetDetailedDiagnosticContextProperties(context);
        }
        catch (Exception ex)
        {
            _diagnosticContext.SetException(ex);
            await SetDetailedDiagnosticContextProperties(context);

            if (_showExceptionInResponse)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    TraceId = Activity.Current?.TraceId.ToString(),
                    RequestId = context.TraceIdentifier
                });
        }
        finally
        {
            SetDiagnosticContextProperties(context);
        }
    }

    private void SetDiagnosticContextProperties(HttpContext context)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "";

        _diagnosticContext.Set("RequestId", context.TraceIdentifier);

        if (context.GetNullableUserId() is not null)
        {
            _diagnosticContext.Set("UserId", context.GetNullableUserId() ?? "");
        }

        _diagnosticContext.Set("Protocol", context.Request.Protocol);
        _diagnosticContext.Set("Ip", ip);
        _diagnosticContext.Set("RequestAborted", context.RequestAborted.IsCancellationRequested);
    }

    private async Task SetDetailedDiagnosticContextProperties(HttpContext context)
    {
        var requestHeaders = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray());

        _diagnosticContext.Set("RequestBody", await ReadRequestBody(context.Request, context.RequestAborted));
        _diagnosticContext.Set("QueryString", context.Request.QueryString.ToString());
        _diagnosticContext.Set("RequestHeaders", requestHeaders);

        var responseHeaders = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray());

        _diagnosticContext.Set("ResponseHeaders", responseHeaders);
    }

    private static async Task<string> ReadRequestBody(HttpRequest request, CancellationToken ct)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(request.Body).ReadToEndAsync(ct);
    }
}