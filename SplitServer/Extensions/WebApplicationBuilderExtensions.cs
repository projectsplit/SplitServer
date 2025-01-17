using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Configuration;

namespace SplitServer.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static AuthenticationBuilder AddAuthentication(
        this WebApplicationBuilder webApplicationBuilder,
        AuthSettings authSettings)
    {
        return webApplicationBuilder
            .Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = authSettings.Issuer,
                        ValidAudience = authSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.Key)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
    }

    public static TSettings Configure<TSettings>(
        this WebApplicationBuilder webApplicationBuilder)
        where TSettings : class, ISettings
    {
        var settings = Activator.CreateInstance<TSettings>();

        webApplicationBuilder.Services.Configure<TSettings>(webApplicationBuilder.Configuration.GetSection(settings.SectionName));

        webApplicationBuilder.Configuration.GetSection(settings.SectionName).Bind(settings);

        return settings;
    }
}