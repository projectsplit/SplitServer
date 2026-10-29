using System.IdentityModel.Tokens.Jwt;
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

    /// <summary>
    /// The session the access token was issued for. The token carries it as the standard "sid"
    /// claim, which the bearer handler may or may not have mapped onto the long-form claim type,
    /// so both spellings are tried.
    /// </summary>
    public static string GetSessionId(this HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(ClaimTypes.Sid) ?? httpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);

        return claim!.Value;
    }
}