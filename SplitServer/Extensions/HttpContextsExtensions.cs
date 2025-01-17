using System.Security.Claims;

namespace SplitServer.Extensions;

public static class HttpContextsExtensions
{
    public static string GetUserId(this HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
    }
}