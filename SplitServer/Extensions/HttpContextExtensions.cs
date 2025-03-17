using System.Security.Claims;

namespace SplitServer.Extensions;

public static class HttpContextExtensions
{
    public static string GetUserId(this HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
    }

    public static string? GetNullableUserId(this HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}