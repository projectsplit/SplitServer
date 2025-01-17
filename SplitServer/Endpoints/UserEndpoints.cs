using MediatR;
using SplitServer.Extensions;
using SplitServer.Queries;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace SplitServer.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", GetAuthenticatedUserHandler);
    }

    private static async Task<IResult> GetAuthenticatedUserHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetAuthenticatedUserQuery(httpContext.GetUserId());

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}