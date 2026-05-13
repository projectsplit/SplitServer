using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Requests;
using SplitServer.Responses;
using SplitServer.Services.Auth;
using SplitServer.Services.Email;

namespace SplitServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/password/sign-up", PasswordSignUpHandler);
        app.MapPost("/password/sign-in", PasswordSignInHandler);
        app.MapPost("/password/forgot", ForgotPasswordHandler);
        app.MapPost("/password/reset", ResetPasswordHandler);
        app.MapPost("/username/forgot", ForgotUsernameHandler);
        app.MapPost("/account/email", SetAccountEmailHandler).RequireAuthorization();
        app.MapPost("/account/email/verify", VerifyAccountEmailHandler).RequireAuthorization();
        app.MapPost("/external/google/token", GoogleTokenHandler);
        app.MapPost("/refresh", RefreshHandler);
        app.MapPost("/log-out", LogOutHandler);
    }

    private static async Task<IResult> GoogleTokenHandler(
        GoogleTokenRequest request,
        IMediator mediator,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken ct)
    {
        var command = new ProcessGoogleCodeCommand
        {
            Code = request.Code
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        authService.AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);

        var response = new PasswordSignInResponse
        {
            AccessToken = result.Value.AccessToken
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> PasswordSignUpHandler(
        PasswordSignUpRequest request,
        IMediator mediator,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken ct)
    {
        var command = new SignUpWithPasswordCommand
        {
            Password = request.Password,
            Username = request.Username,
            Email = request.Email
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        authService.AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);

        var response = new PasswordSignUpResponse
        {
            AccessToken = result.Value.AccessToken
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> PasswordSignInHandler(
        PasswordSignInRequest request,
        IMediator mediator,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken ct)
    {
        var command = new SignInWithPasswordCommand
        {
            Username = request.Username,
            Password = request.Password
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        authService.DeleteRefreshTokenCookie(httpContext);
        authService.AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);

        var response = new PasswordSignInResponse
        {
            AccessToken = result.Value.AccessToken
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> ForgotPasswordHandler(
        ForgotPasswordRequest request,
        IMediator mediator,
        HttpContext httpContext,
        EmailThrottleService throttleService,
        CancellationToken ct)
    {
        var ip = GetClientIp(httpContext);

        if (!throttleService.TryConsume("password-forgot", ip, request.Email ?? ""))
        {
            return Results.Ok();
        }

        await mediator.Send(new RequestPasswordResetCommand { Email = request.Email ?? "" }, ct);

        return Results.Ok();
    }

    private static async Task<IResult> ResetPasswordHandler(
        ResetPasswordRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ResetPasswordCommand
        {
            Token = request.Token,
            NewPassword = request.NewPassword,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> ForgotUsernameHandler(
        ForgotUsernameRequest request,
        IMediator mediator,
        HttpContext httpContext,
        EmailThrottleService throttleService,
        CancellationToken ct)
    {
        var ip = GetClientIp(httpContext);

        if (!throttleService.TryConsume("username-forgot", ip, request.Email ?? ""))
        {
            return Results.Ok();
        }

        await mediator.Send(new RequestUsernameRecoveryCommand { Email = request.Email ?? "" }, ct);

        return Results.Ok();
    }

    private static async Task<IResult> SetAccountEmailHandler(
        SetAccountEmailRequest request,
        IMediator mediator,
        HttpContext httpContext,
        EmailThrottleService throttleService,
        CancellationToken ct)
    {
        var ip = GetClientIp(httpContext);
        var userId = httpContext.GetUserId();

        if (!throttleService.TryConsume("set-email", ip, userId))
        {
            return Results.BadRequest("Too many requests. Please try again later.");
        }

        var command = new SetAccountEmailCommand
        {
            UserId = userId,
            Email = request.Email,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> VerifyAccountEmailHandler(
        VerifyAccountEmailRequest request,
        IMediator mediator,
        HttpContext httpContext,
        EmailThrottleService throttleService,
        CancellationToken ct)
    {
        var ip = GetClientIp(httpContext);
        var userId = httpContext.GetUserId();

        if (!throttleService.TryConsume("verify-email", ip, userId))
        {
            return Results.BadRequest("Too many requests. Please try again later.");
        }

        var command = new VerifyAccountEmailCommand
        {
            UserId = userId,
            Code = request.Code,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> RefreshHandler(
        HttpContext httpContext,
        IMediator mediator,
        AuthService authService,
        CancellationToken ct)
    {
        var refreshTokenResult = authService.GetRefreshTokenCookie(httpContext);

        if (refreshTokenResult.IsFailure)
        {
            return Results.Unauthorized();
        }

        Console.WriteLine(refreshTokenResult.Value);

        var command = new RefreshCommand
        {
            RefreshToken = refreshTokenResult.Value
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return Results.Unauthorized();
        }

        authService.DeleteRefreshTokenCookie(httpContext);
        authService.AppendRefreshTokenCookie(httpContext, result.Value.RefreshToken);

        var response = new RefreshResponse
        {
            AccessToken = result.Value.AccessToken
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> LogOutHandler(
        HttpContext httpContext,
        IMediator mediator,
        AuthService authService,
        CancellationToken ct)
    {
        var refreshTokenResult = authService.GetRefreshTokenCookie(httpContext);

        if (refreshTokenResult.IsFailure)
        {
            return Results.Ok();
        }

        var command = new LogOutCommand
        {
            RefreshToken = refreshTokenResult.Value
        };

        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            Console.WriteLine(result.Error);
        }

        authService.DeleteRefreshTokenCookie(httpContext);

        return Results.Ok();
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
