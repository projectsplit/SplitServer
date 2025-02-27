using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Services;

namespace SplitServer.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/password/sign-up", PasswordSignUpHandler);
        app.MapPost("/password/sign-in", PasswordSignInHandler);
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
        var command = new ProcessGoogleAccessTokenCommand
        {
            GoogleAccessToken = request.GoogleAccessToken
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
            Username = request.Username
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
}