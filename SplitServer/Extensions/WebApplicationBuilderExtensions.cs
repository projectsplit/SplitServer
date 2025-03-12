using SplitServer.Configuration;

namespace SplitServer.Extensions;

public static class WebApplicationBuilderExtensions
{
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