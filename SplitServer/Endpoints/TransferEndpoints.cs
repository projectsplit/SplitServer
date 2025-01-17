using MediatR;
using SplitServer.Commands;
using SplitServer.Dto;
using SplitServer.Extensions;
using SplitServer.Queries;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace SplitServer.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateTransferHandler);
        app.MapPost("/delete", DeleteTransferHandler);
        app.MapGet("/", GetGroupTransfersHandler);
        // app.MapPost("/update", UpdateTransferHandler);
    }

    private static async Task<IResult> CreateTransferHandler(
        CreateTransferRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateTransferCommand(
            httpContext.GetUserId(),
            request.GroupId,
            request.Amount,
            request.Currency,
            request.Description,
            request.Occured,
            request.SenderId,
            request.ReceiverId);

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteTransferHandler(
        DeleteTransferRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteTransferCommand(httpContext.GetUserId(), request.TransferId);
    
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
        var command = new GetGroupTransfersQuery(httpContext.GetUserId(), groupId, pageSize, next);
    
        var result = await mediator.Send(command, ct);
    
        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    // private static async Task<IResult> UpdateTransferHandler(
    //     UpdateTransferRequest request,
    //     IMediator mediator,
    //     HttpContext httpContext,
    //     CancellationToken ct)
    // {
    //     var command = new UpdateTransferCommand(httpContext.GetUserId(), request.TransferId, request.Name, request.Currency);
    //
    //     var result = await mediator.Send(command, ct);
    //
    //     return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    // }
}