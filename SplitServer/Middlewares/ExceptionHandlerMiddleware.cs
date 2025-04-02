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
        }
        catch (ResourceLockedException)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = MediaTypeNames.Application.Json;
        }
        catch (Exception ex)
        {
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "";
            var headers = string.Join(" \n", context.Request.Headers.Select(h => $"{h.Key}: {h.Value.ToString()}"));
            var userId = context.GetNullableUserId() ?? "";

            _diagnosticContext.Set("RequestBody", await ReadRequestBodyAsync(context.Request));
            _diagnosticContext.Set("RequestId", context.TraceIdentifier);
            _diagnosticContext.Set("UserId", userId);
            _diagnosticContext.Set("QueryString", context.Request.QueryString.ToString());
            _diagnosticContext.Set("Headers", headers);
            _diagnosticContext.Set("Protocol", context.Request.Protocol);
            _diagnosticContext.Set("Ip", ip);
            _diagnosticContext.SetException(ex);

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
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(request.Body).ReadToEndAsync();
    }
}