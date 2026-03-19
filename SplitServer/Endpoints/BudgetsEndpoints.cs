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
        app.MapPost("/create", CreateBudget);
        app.MapGet("/get-active", GetActiveBudgetInfo);

    }

    private static async Task<IResult> GetActiveBudgetInfo( 
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

    private static async Task<IResult> CreateBudget(
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
}