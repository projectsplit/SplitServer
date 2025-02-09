using MediatR;
using SplitServer.Extensions;
using SplitServer.Queries;

namespace SplitServer.Endpoints;

public static class DebtEndpoints
{
    public static void MapDebtEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetGroupDebtsHandler);
    }

    private static async Task<IResult> GetGroupDebtsHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetGroupDebtsQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}