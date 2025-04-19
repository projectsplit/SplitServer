using System.Reflection;
using Microsoft.AspNetCore.Http.Json;
using Serilog;
using SplitServer.Configuration;
using SplitServer.Endpoints;
using SplitServer.Extensions;
using SplitServer.HttpClientHandlers;
using SplitServer.Middlewares;
using SplitServer.Repositories;
using SplitServer.Repositories.Implementations;
using SplitServer.Services;
using SplitServer.Services.Auth;
using SplitServer.Services.CurrencyExchangeRate;
using SplitServer.Services.OpenExchangeRates;
using SplitServer.Services.TimeZone;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
{
    clientBuilder.AddHttpMessageHandler<HttpClientLoggingHandler>();
});

builder.Services.AddTransient<HttpClientLoggingHandler>();

builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.AllowTrailingCommas = true; });
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ValidationService>();
builder.Services.AddSingleton<PermissionService>();
builder.Services.AddSingleton<LockService>();
builder.Services.AddSingleton<GroupService>();
builder.Services.AddSingleton<CurrencyExchangeRateService>();
builder.Services.AddSingleton<ExceptionHandlerMiddleware>();
builder.Services.AddSingleton<OpenExchangeRatesClient>();
builder.Services.AddSingleton<TimeZoneService>();

builder.Services.AddSingleton<IMongoConnection, MongoConnection>();
builder.Services.AddSingleton<IUsersRepository, UsersMongoDbRepository>();
builder.Services.AddSingleton<ISessionsRepository, SessionsMongoDbRepository>();
builder.Services.AddSingleton<IGroupsRepository, GroupsMongoDbRepository>();
builder.Services.AddSingleton<IExpensesRepository, ExpensesMongoDbRepository>();
builder.Services.AddSingleton<ITransfersRepository, TransfersMongoDbRepository>();
builder.Services.AddSingleton<IInvitationsRepository, InvitationsMongoDbRepository>();
builder.Services.AddSingleton<IJoinCodesRepository, JoinCodesMongoDbRepository>();
builder.Services.AddSingleton<ICurrencyExchangeRatesRepository, CurrencyExchangeRatesMongoDbRepository>();
builder.Services.AddSingleton<IUserActivityRepository, UserActivityMongoDbRepository>();
builder.Services.AddSingleton<IUserPreferencesRepository, UserPreferencesMongoDbRepository>();

builder.Configure<MongoDbSettings>();
builder.Configure<JoinSettings>();
builder.Configure<OpenExchangeRatesSettings>();
builder.Configure<ErrorHandlingSettings>();
var openTelemetrySettings = builder.Configure<OpenTelemetrySettings>();
var authSettings = builder.Configure<AuthSettings>();
builder.Services.AddAuthentication(authSettings);
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(
    corsOptions =>
    {
        corsOptions.AddDefaultPolicy(
            policyBuilder =>
            {
                policyBuilder
                    .WithOrigins(authSettings.ClientUrl)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });

builder.ConfigureLogging(openTelemetrySettings);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();
app.Run();