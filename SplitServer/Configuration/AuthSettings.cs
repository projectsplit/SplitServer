namespace SplitServer.Configuration;

public class AuthSettings : ISettings
{
    public required string SectionName { get; init; } = "Auth";
    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int AccessTokenDurationInMinutes { get; init; }
    public required int RefreshTokenDurationInMinutes { get; init; }
    public required string RefreshTokenCookieName { get; init; }
    public required string RefreshEndpointPath { get; init; }
    public required bool RefreshTokenCookieSecure { get; init; }
    public required bool RefreshTokenCookieHttpOnly { get; init; }
    public required SameSiteMode RefreshTokenCookieSameSite { get; init; }
    public required string ClientUrl { get; init; }
    public required string ClientGoogleRedirectUri { get; init; }
    public required string GoogleTokenEndpoint { get; init; }
    public required string GoogleClientId { get; init; }
    public required string GoogleClientSecret { get; init; }
    public required string GoogleJsonWebKeySetEndpoint { get; init; }
    public required string GoogleIdTokenIssuer { get; init; }
    public required string ApiKey { get; init; }
    public required bool AllowPasswordSignup { get; init; }
}