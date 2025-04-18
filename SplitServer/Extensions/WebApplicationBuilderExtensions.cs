using System.Reflection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
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

    public static WebApplicationBuilder ConfigureLogging(
        this WebApplicationBuilder webApplicationBuilder,
        OpenTelemetrySettings openTelemetrySettings)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Fatal)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);

        if (openTelemetrySettings.Enabled)
        {
            loggerConfiguration
                .WriteTo.OpenTelemetry(
                    options =>
                    {
                        options.Endpoint = openTelemetrySettings.Endpoint;
                        options.Protocol = OtlpProtocol.HttpProtobuf;
                        options.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.version"] = Assembly.GetExecutingAssembly().GetName().Version!.ToString(),
                            ["service.name"] = Assembly.GetExecutingAssembly().GetName().Name!,
                            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!,
                        };
                    });
        }

        Log.Logger = loggerConfiguration
            .WriteTo.Console()
            .CreateBootstrapLogger();

        webApplicationBuilder.Host.UseSerilog();

        return webApplicationBuilder;
    }
}