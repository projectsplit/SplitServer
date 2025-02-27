using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Configuration;
using SplitServer.Models;

namespace SplitServer.Services;

public class AuthService
{
    private readonly AuthSettings _authSettings;

    public AuthService(IOptions<AuthSettings> jwtSettingsOptions)
    {
        _authSettings = jwtSettingsOptions.Value;
    }

    public Result<string> GetRefreshTokenCookie(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(_authSettings.RefreshTokenCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure<string>("Refresh token cookie not found or invalid");
        }

        return refreshToken;
    }

    public void DeleteRefreshTokenCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            _authSettings.RefreshTokenCookieName,
            new CookieOptions
            {
                Path = _authSettings.RefreshEndpointPath,
                Secure = _authSettings.RefreshTokenCookieSecure,
                HttpOnly = _authSettings.RefreshTokenCookieHttpOnly,
                SameSite = _authSettings.RefreshTokenCookieSameSite
            });
    }

    public void AppendRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        httpContext.Response.Cookies.Append(
            _authSettings.RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                Path = _authSettings.RefreshEndpointPath,
                HttpOnly = _authSettings.RefreshTokenCookieHttpOnly,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_authSettings.RefreshTokenDurationInMinutes),
                MaxAge = TimeSpan.FromMinutes(_authSettings.RefreshTokenDurationInMinutes),
                Secure = _authSettings.RefreshTokenCookieSecure,
                SameSite = _authSettings.RefreshTokenCookieSameSite
            });
    }

    public string GenerateAccessToken(string userId, string sessionId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Iss, _authSettings.Issuer),
            new(JwtRegisteredClaimNames.Nonce, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sid, sessionId),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _authSettings.Issuer,
            audience: _authSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(_authSettings.AccessTokenDurationInMinutes)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    public bool HasSessionExpired(Session session)
    {
        return DateTime.UtcNow > session.Created + TimeSpan.FromMinutes(_authSettings.RefreshTokenDurationInMinutes);
    }
}