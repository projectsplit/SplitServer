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
                await SetDetailedRequestDiagnosticContextProperties(context);
            }
        }
        catch (BadHttpRequestException ex)
        {
            _diagnosticContext.SetException(ex);

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
        }
        catch (ResourceLockedException ex)
        {
            _diagnosticContext.SetException(ex);

            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = MediaTypeNames.Application.Json;
        }
        catch (Exception ex)
        {
            _diagnosticContext.SetException(ex);
            await SetDetailedRequestDiagnosticContextProperties(context);

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
        _diagnosticContext.Set("UserId", context.GetNullableUserId() ?? "");
        _diagnosticContext.Set("Protocol", context.Request.Protocol);
        _diagnosticContext.Set("Ip", ip);
    }

    private async Task SetDetailedRequestDiagnosticContextProperties(HttpContext context)
    {
        var headers = string.Join(" \n", context.Request.Headers.Select(h => $"{h.Key}: {h.Value.ToString()}"));

        _diagnosticContext.Set("RequestBody", await ReadRequestBodyAsync(context.Request, context.RequestAborted));
        _diagnosticContext.Set("QueryString", context.Request.QueryString.ToString());
        _diagnosticContext.Set("Headers", headers);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(request.Body).ReadToEndAsync(ct);
    }
}