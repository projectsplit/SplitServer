using System.Net.Mime;
using System.Text.Json;
using SplitServer.Services;

namespace SplitServer.Middlewares;

public class ExceptionHandlerMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
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
    }
}