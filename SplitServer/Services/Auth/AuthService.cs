using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Configuration;
using SplitServer.Models;
using SplitServer.Services.Auth.Models;

namespace SplitServer.Services.Auth;

public class AuthService
{
    private readonly AuthSettings _authSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string ApiKeyHeaderName = "api-key";

    public AuthService(
        IOptions<AuthSettings> jwtSettingsOptions,
        IHttpClientFactory httpClientFactory)
    {
        _authSettings = jwtSettingsOptions.Value;
        _httpClientFactory = httpClientFactory;
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

    public async Task<Result<GoogleUserInfo>> GetGoogleUserInfo(string code, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, _authSettings.GoogleTokenEndpoint);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", _authSettings.GoogleClientId },
                { "client_secret", _authSettings.GoogleClientSecret },
                { "redirect_uri", $"{_authSettings.ClientUrl}{_authSettings.ClientGoogleRedirectUri}" },
                { "grant_type", "authorization_code" }
            });

        var tokenHttpResponse = await client.SendAsync(request, ct);

        if (!tokenHttpResponse.IsSuccessStatusCode)
        {
            return Result.Failure<GoogleUserInfo>("Google id token could not be retrieved");
        }

        var tokenResponse = await tokenHttpResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct);
        var idToken = tokenResponse!.IdToken;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = await GetGoogleJsonWebKeySet(ct),
            ValidateIssuer = true,
            ValidIssuer = _authSettings.GoogleIdTokenIssuer,
            ValidateAudience = true,
            ValidAudience = _authSettings.GoogleClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        var tokenValidationResult = await handler.ValidateTokenAsync(idToken, validationParameters);

        if (!tokenValidationResult.IsValid || tokenValidationResult.SecurityToken is not JwtSecurityToken jwtSecurityToken)
        {
            return Result.Failure<GoogleUserInfo>("Google idToken is not valid");
        }

        return new GoogleUserInfo
        {
            Id = jwtSecurityToken.Subject,
            Email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value!,
            Name = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value
        };
    }

    private async Task<IList<JsonWebKey>> GetGoogleJsonWebKeySet(CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var json = await httpClient.GetStringAsync(_authSettings.GoogleJsonWebKeySetEndpoint, ct);
        return JsonSerializer.Deserialize<JsonWebKeySet>(json)!.Keys;
    }

    public Result ValidateApiKey(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            apiKey != _authSettings.ApiKey)
        {
            return Result.Failure("Api key is missing or invalid");
        }

        return Result.Success();
    }
}