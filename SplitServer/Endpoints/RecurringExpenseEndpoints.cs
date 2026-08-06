using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;
using SplitServer.Services.Auth;

namespace SplitServer.Endpoints;

public static class RecurringExpenseEndpoints
{
    public static void MapRecurringExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetRecurringExpensesHandler);
        app.MapPost("/create", CreateRecurringExpenseHandler);
        app.MapPost("/edit", EditRecurringExpenseHandler);
        app.MapPost("/delete", DeleteRecurringExpenseHandler);
        app.MapPost("/toggle-status", ToggleRecurringExpenseStatusHandler);
        app.MapPost("/process", ProcessDueRecurringExpensesHandler).AllowAnonymous();
    }

    private static async Task<IResult> GetRecurringExpensesHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetRecurringExpensesQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateRecurringExpenseHandler(
        CreateRecurringExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateRecurringExpenseCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Schedule = request.Schedule,
            Payments = request.Payments,
            Shares = request.Shares,
            NonGroupPayments = request.NonGroupPayments,
            NonGroupShares = request.NonGroupShares,
            Labels = request.Labels,
            Location = request.Location,
            LinkExpenseId = request.LinkExpenseId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> EditRecurringExpenseHandler(
        EditRecurringExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditRecurringExpenseCommand
        {
            UserId = httpContext.GetUserId(),
            RecurringExpenseId = request.RecurringExpenseId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Schedule = request.Schedule,
            Payments = request.Payments,
            Shares = request.Shares,
            NonGroupPayments = request.NonGroupPayments,
            NonGroupShares = request.NonGroupShares,
            Labels = request.Labels,
            Location = request.Location
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DeleteRecurringExpenseHandler(
        RecurringExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteRecurringExpenseCommand
        {
            UserId = httpContext.GetUserId(),
            RecurringExpenseId = request.RecurringExpenseId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> ToggleRecurringExpenseStatusHandler(
        RecurringExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new ToggleRecurringExpenseStatusCommand
        {
            UserId = httpContext.GetUserId(),
            RecurringExpenseId = request.RecurringExpenseId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    /// <summary>
    /// Same pass the in-app worker runs, exposed for forcing a run by hand or from an external
    /// scheduler. Api key protected like the historical rates job, never a user session: it acts on
    /// every account's templates, not the caller's.
    /// </summary>
    private static async Task<IResult> ProcessDueRecurringExpensesHandler(
        AuthService authService,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (authService.ValidateApiKey(httpContext).IsFailure)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new ProcessDueRecurringExpensesCommand(), ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}
