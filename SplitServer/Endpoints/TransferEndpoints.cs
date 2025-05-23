﻿using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateTransferHandler);
        app.MapPost("/create-many", CreateManyTransfersHandler);
        app.MapPost("/delete", DeleteTransferHandler);
        app.MapGet("/", GetGroupTransfersHandler);
        app.MapPost("/edit", EditTransferHandler);
    }

    private static async Task<IResult> CreateTransferHandler(
        CreateTransferRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateTransferCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Occurred = request.Occurred,
            SenderId = request.SenderId,
            ReceiverId = request.ReceiverId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateManyTransfersHandler(
        CreateManyTransfersRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateManyTransfersCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId,
            Transfers = request.Transfers
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> DeleteTransferHandler(
        DeleteTransferRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteTransferCommand
        {
            UserId = httpContext.GetUserId(),
            TransferId = request.TransferId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupTransfersHandler(
        string groupId,
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupTransfersQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            PageSize = pageSize,
            Next = next
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> EditTransferHandler(
        EditTransferRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditTransferCommand
        {
            TransferId = request.TransferId,
            UserId = httpContext.GetUserId(),
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Occurred = request.Occurred,
            SenderId = request.SenderId,
            ReceiverId = request.ReceiverId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}