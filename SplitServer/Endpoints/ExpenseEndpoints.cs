using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Extensions;
using SplitServer.Queries;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace SplitServer.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateExpenseHandler);
        app.MapPost("/delete", DeleteExpenseHandler);
        app.MapGet("/", GetGroupExpensesHandler);
        app.MapGet("/labels", GetLabelsHandler);
        // app.MapPost("/update", UpdateExpenseHandler);
    }

    private static async Task<IResult> CreateExpenseHandler(
        CreateExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateExpenseCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Occured = request.Occured,
            Payments = request.Payments,
            Shares = request.Shares,
            Labels = request.Labels,
            Location = request.Location
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteExpenseHandler(
        DeleteExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteExpenseCommand(httpContext.GetUserId(), request.ExpenseId);
    
        var result = await mediator.Send(command, ct);
    
        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupExpensesHandler(
        string groupId,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetGroupExpensesQuery(httpContext.GetUserId(), groupId, pageSize, next);
    
        var result = await mediator.Send(command, ct);
    
        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    // private static async Task<IResult> UpdateExpenseHandler(
    //     UpdateExpenseRequest request,
    //     IMediator mediator,
    //     HttpContext httpContext,
    //     CancellationToken ct)
    // {
    //     var command = new UpdateExpenseCommand(httpContext.GetUserId(), request.ExpenseId, request.Name, request.Currency);
    //
    //     var result = await mediator.Send(command, ct);
    //
    //     return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    // }

    private static async Task<IResult> GetLabelsHandler(
        string groupId,
        int limit,
        string? query,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new GetLabelsQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            Limit = limit,
            Query = query
        };
    
        var result = await mediator.Send(command, ct);
    
        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}