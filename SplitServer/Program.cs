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
using SplitServer.Services.Donations;
using SplitServer.Services.Email;
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
builder.Services.AddSingleton<GoogleAccountService>();
builder.Services.AddSingleton<ValidationService>();
builder.Services.AddSingleton<PermissionService>();
builder.Services.AddSingleton<LockService>();
builder.Services.AddSingleton<GroupService>();
builder.Services.AddSingleton<NonGroupService>();
builder.Services.AddSingleton<ConnectionService>();
builder.Services.AddSingleton<PushNotificationService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<UserLabelService>();
builder.Services.AddSingleton< BudgetService>();
builder.Services.AddSingleton<RecurringExpenseValidator>();
builder.Services.AddSingleton<CurrencyExchangeRateService>();
builder.Services.AddSingleton<ExceptionHandlerMiddleware>();
builder.Services.AddSingleton<OpenExchangeRatesClient>();
builder.Services.AddSingleton<TimeZoneService>();
builder.Services.AddSingleton<DonationPromptPolicy>();
builder.Services.AddSingleton<StripeDonationService>();

builder.Services.AddSingleton<IMongoConnection, MongoConnection>();
builder.Services.AddSingleton<IUsersRepository, UsersMongoDbRepository>();
builder.Services.AddSingleton<ISessionsRepository, SessionsMongoDbRepository>();
builder.Services.AddSingleton<IGroupsRepository, GroupsMongoDbRepository>();
builder.Services.AddSingleton<IExpensesRepository, ExpensesMongoDbRepository>();
builder.Services.AddSingleton<IRecurringExpensesRepository, RecurringExpensesMongoDbRepository>();
builder.Services.AddSingleton<ITransfersRepository, TransfersMongoDbRepository>();
builder.Services.AddSingleton<IInvitationsRepository, InvitationsMongoDbRepository>();
builder.Services.AddSingleton<IJoinCodesRepository, JoinCodesMongoDbRepository>();
builder.Services.AddSingleton<ICurrencyExchangeRatesRepository, CurrencyExchangeRatesMongoDbRepository>();
builder.Services.AddSingleton<IUserActivityRepository, UserActivityMongoDbRepository>();
builder.Services.AddSingleton<IUserPreferencesRepository, UserPreferencesMongoDbRepository>();
builder.Services.AddSingleton<IUserLabelsRepository, UserLabelsMongoDbRepository>();
builder.Services.AddSingleton<IBudgetsRepository, BudgetsMongoDbRepository>();
builder.Services.AddSingleton<IEmailVerificationCodesRepository, EmailVerificationCodesMongoDbRepository>();
builder.Services.AddSingleton<IPushSubscriptionsRepository, PushSubscriptionsMongoDbRepository>();
builder.Services.AddSingleton<INotificationsRepository, NotificationsMongoDbRepository>();
builder.Services.AddSingleton<IUserConnectionsRepository, UserConnectionsMongoDbRepository>();
builder.Services.AddSingleton<IDonationsRepository, DonationsMongoDbRepository>();
builder.Services.AddSingleton<IDonationSubscriptionsRepository, DonationSubscriptionsMongoDbRepository>();
builder.Services.AddSingleton<IDonationPromptStatesRepository, DonationPromptStatesMongoDbRepository>();

builder.Services.AddHostedService<RecurringExpensesWorker>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailTokenService>();
builder.Services.AddSingleton<EmailThrottleService>();

builder.Configure<MongoDbSettings>();
builder.Configure<JoinSettings>();
builder.Configure<OpenExchangeRatesSettings>();
builder.Configure<ErrorHandlingSettings>();
builder.Configure<PushNotificationsSettings>();
builder.Configure<StripeSettings>();
builder.Configure<DonationsSettings>();
var openTelemetrySettings = builder.Configure<OpenTelemetrySettings>();
var authSettings = builder.Configure<AuthSettings>();
var emailSettings = builder.Configure<EmailSettings>();

if (emailSettings.Enabled)
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

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
                    // The Android app serves the same bundle from inside its own shell, where the
                    // origin is fixed at https://localhost by Capacitor rather than being our domain.
                    // Every request it makes is cross-origin, so without this the app gets nothing
                    // past the preflight. It is not a wildcard: only the app's WebView is this origin.
                    .WithOrigins(authSettings.ClientUrl, "https://localhost")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });

builder.ConfigureLogging(openTelemetrySettings);

var app = builder.Build();

// Index creation is idempotent, so this just converges the collection on every boot rather than
// needing a migration step. Never fatal: the app works without the indexes, only slower and
// without expiry.
try
{
    await app.Services.GetRequiredService<INotificationsRepository>().EnsureIndexes(CancellationToken.None);
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to ensure notification indexes");
}

try
{
    await app.Services.GetRequiredService<IRecurringExpensesRepository>().EnsureIndexes(CancellationToken.None);
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to ensure recurring expense indexes");
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();
app.Run();