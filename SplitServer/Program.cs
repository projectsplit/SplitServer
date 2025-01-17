using System.Reflection;
using Microsoft.AspNetCore.Http.Json;
using SplitServer.Configuration;
using SplitServer.Endpoints;
using SplitServer.Extensions;
using SplitServer.Middlewares;
using SplitServer.Repositories;
using SplitServer.Repositories.Implementations;
using SplitServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configure<MongoDbSettings>();
builder.Services.AddSingleton<IMongoConnection, MongoConnection>();
builder.Services.AddSingleton<IUsersRepository, UsersMongoDbRepository>();
builder.Services.AddSingleton<ISessionsRepository, SessionsMongoDbRepository>();
builder.Services.AddSingleton<IInvitationsRepository, InvitationsMongoDbRepository>();
builder.Services.AddSingleton<IGroupsRepository, GroupsMongoDbRepository>();
builder.Services.AddSingleton<IExpensesRepository, ExpensesMongoDbRepository>();
builder.Services.AddSingleton<ITransfersRepository, TransfersMongoDbRepository>();

builder.Services.AddHttpClient();
builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.AllowTrailingCommas = true; });
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
builder.Services.AddSingleton<ValidationService>();
builder.Services.AddSingleton<LockService>();
builder.Services.AddSingleton<ExceptionHandlerMiddleware>();

var authSettings = builder.Configure<AuthSettings>();
builder.Services.AddSingleton<AuthService>();
builder.AddAuthentication(authSettings);
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

var app = builder.Build();

app.UseCors();
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
// app.UseHttpsRedirection();
app.MapGroup("/auth").MapAuthEndpoints();
app.MapGroup("/users").RequireAuthorization().MapUserEndpoints();
app.MapGroup("/groups").RequireAuthorization().MapGroupEndpoints();
app.MapGroup("/expenses").RequireAuthorization().MapExpenseEndpoints();
app.MapGroup("/transfers").RequireAuthorization().MapTransferEndpoints();
app.MapGroup("/debts").RequireAuthorization().MapDebtEndpoints();
app.MapGroup("/invitations").RequireAuthorization().MapInvitationEndpoints();
app.MapGet("/", (HttpContext context) => new { UserId = context.GetUserId() }).RequireAuthorization();
app.Run();