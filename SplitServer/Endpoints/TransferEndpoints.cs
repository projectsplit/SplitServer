using MediatR;
using Microsoft.IdentityModel.Tokens;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;
using SplitServer.Responses;

namespace SplitServer.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", GetGroupTransfersHandler);
        app.MapPost("/create", CreateTransferHandler);
        app.MapPost("/create-many", CreateManyTransfersHandler);
        app.MapPost("/delete", DeleteTransferHandler);
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
        HttpContext httpContext,
        IMediator mediator,
        string groupId,
        int pageSize,
        string? next,
        DateTime? before,
        DateTime? after,
        string? searchTerm,
        string[]? receiverIds,
        string[]? senderIds,
        CancellationToken ct)
    {
        var hasAnySearchParams = before is not null ||
                                 after is not null ||
                                 searchTerm is not null ||
                                 !receiverIds.IsNullOrEmpty() ||
                                 !senderIds.IsNullOrEmpty();

        IRequest<CSharpFunctionalExtensions.Result<GroupTransfersResponse>> query = hasAnySearchParams
            ? new SearchGroupTransfersQuery
            {
                UserId = httpContext.GetUserId(),
                GroupId = groupId,
                PageSize = pageSize,
                Next = next,
                Before = before,
                After = after,
                SearchTerm = searchTerm,
                ReceiverIds = receiverIds,
                SenderIds = senderIds
            }
            : new GetGroupTransfersQuery
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