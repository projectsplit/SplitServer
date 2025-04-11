using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class DebtEndpoints
{
    public static void MapDebtEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetGroupDebtsHandler);
        app.MapPost("/settle-guest", SettleGuestDebtHandler);
    }

    private static async Task<IResult> GetGroupDebtsHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        
        var query = new GetGroupDebtsQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SettleGuestDebtHandler(
        SettleGuestDebtRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new SettleGuestDebtCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            GuestId = request.GuestId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}