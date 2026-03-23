using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class BudgetsEndpoints
{
    public static void MapBudgetsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/get-inactive", GetInactiveBudgetsInfoHandler);
        app.MapGet("/get-active", GetActiveBudgetInfoHandler);
        app.MapPost("/create", CreateBudgetHandler);
        app.MapPost("/edit", EditBudgetHandler);
        app.MapPost("/toggle-status", ToggleBudgetStatusHandler);
        app.MapPost("/delete", DeleteBudgetHandler);
    }

    private static async Task<IResult> GetInactiveBudgetsInfoHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetInactiveBudgetsInfoQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> ToggleBudgetStatusHandler(
        ToggleBudgetStatusRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new ToggleBudgetStatusCommand
        {
            UserId = httpContext.GetUserId(),
            BudgetId = request.BudgetId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetActiveBudgetInfoHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetActiveBudgetInfoQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateBudgetHandler(
        CreateBudgetRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateBudgetCommand
        {
            UserId = httpContext.GetUserId(),
            Description = request.Description,
            Amount = request.Amount,
            Currency = request.Currency,
            Frequency = request.Frequency,
            Scope = request.Scope,
            Activate = request.Activate,
            CommencementDay = request.CommencementDay,
            TargetGroupIds = request.TargetGroupIds,
            EndDate = request.EndDate,
            StartDate = request.StartDate
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> EditBudgetHandler(
        EditBudgetRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditBudgetCommand
        {
            UserId = httpContext.GetUserId(),
            BudgetId = request.BudgetId,
            Description = request.Description,
            Amount = request.Amount,
            Currency = request.Currency,
            Frequency = request.Frequency,
            Scope = request.Scope,
            Activate = request.Activate,
            CommencementDay = request.CommencementDay,
            TargetGroupIds = request.TargetGroupIds,
            EndDate = request.EndDate,
            StartDate = request.StartDate
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DeleteBudgetHandler(
        DeleteBudgetRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteBudgetCommand
        {
            UserId = httpContext.GetUserId(),
            BudgetId = request.BudgetId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}