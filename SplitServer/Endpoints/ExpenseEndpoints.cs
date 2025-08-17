using MediatR;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;
using SplitServer.Responses;

namespace SplitServer.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetGroupExpensesHandler);
        app.MapGet("/search", SearchExpensesHandler);
        app.MapGet("/labels", GetLabelsHandler);
        app.MapPost("/create", CreateExpenseHandler);
        app.MapPost("/delete", DeleteExpenseHandler);
        app.MapPost("/edit", EditExpenseHandler);
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
            Occurred = request.Occurred,
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
        var command = new DeleteExpenseCommand
        {
            UserId = httpContext.GetUserId(),
            ExpenseId = request.ExpenseId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupExpensesHandler(
        HttpContext httpContext,
        IMediator mediator,
        string groupId,
        int pageSize,
        string? next,
        DateTime? before,
        DateTime? after,
        string? searchTerm,
        string[]? labelIds,
        string[]? participantIds,
        string[]? payerIds,
        CancellationToken ct)
    {
        var hasAnySearchParams = before is not null ||
                                 after is not null ||
                                 searchTerm is not null ||
                                 !labelIds.IsNullOrEmpty() ||
                                 !participantIds.IsNullOrEmpty() ||
                                 !payerIds.IsNullOrEmpty();

        IRequest<CSharpFunctionalExtensions.Result<GroupExpensesResponse>> query = hasAnySearchParams
            ? new SearchGroupExpensesQuery
            {
                UserId = httpContext.GetUserId(),
                GroupId = groupId,
                Before = before,
                After = after,
                SearchTerm = searchTerm,
                LabelIds = labelIds,
                ParticipantIds = participantIds,
                PayerIds = payerIds,
                PageSize = pageSize,
                Next = next,
            }
            : new GetGroupExpensesQuery
            {
                UserId = httpContext.GetUserId(),
                GroupId = groupId,
                PageSize = pageSize,
                Next = next
            };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> SearchExpensesHandler(
        string groupId,
        DateTime? before,
        DateTime? after,
        string? searchTerm,
        string[]? labelIds,
        string[]? participantIds,
        string[]? payerIds,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new SearchGroupExpensesQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            Before = before,
            After = after,
            SearchTerm = searchTerm,
            LabelIds = labelIds,
            ParticipantIds = participantIds,
            PayerIds = payerIds,
            PageSize = pageSize,
            Next = next,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetLabelsHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetLabelsQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> EditExpenseHandler(
        EditExpenseRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditExpenseCommand
        {
            ExpenseId = request.ExpenseId,
            UserId = httpContext.GetUserId(),
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Occurred = request.Occurred,
            Payments = request.Payments,
            Shares = request.Shares,
            Labels = request.Labels,
            Location = request.Location
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}