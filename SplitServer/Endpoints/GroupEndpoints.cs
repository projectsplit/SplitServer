using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;
using SplitServer.Requests;

namespace SplitServer.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create", CreateGroupHandler);
        app.MapPost("/delete", DeleteGroupHandler);
        app.MapGet("/{groupId}", GetGroupHandler);
        app.MapPost("/{groupId}/add-guest", AddGuestHandler);
        app.MapPost("/{groupId}/remove-member", RemoveMemberHandler);
        app.MapPost("/{groupId}/remove-guest", RemoveGuestHandler);
        app.MapPost("/{groupId}/leave", LeaveGroupHandler);
        app.MapPut("/{groupId}/name", EditGroupNameHandler);
        app.MapPut("/{groupId}/currency", EditGroupCurrencyHandler);
        app.MapPut("/{groupId}/archive", EditGroupArchiveStatusHandler);
        app.MapGet("/", GetGroupsHandler);
        app.MapGet("/details", GetGroupsWithDetailsHandler);
        app.MapGet("/{groupId}/details", GetGroupDetailsHandler);
        app.MapGet("/all-balances", GetAllGroupsBalancesHandler);
    }

    private static async Task<IResult> CreateGroupHandler(
        CreateGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new CreateGroupCommand
        {
            UserId = httpContext.GetUserId(),
            Name = request.Name,
            Currency = request.Currency
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteGroupHandler(
        DeleteGroupRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new DeleteGroupCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = request.GroupId
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetGroupsHandler(
        int pageSize,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupsQuery
        {
            UserId = httpContext.GetUserId(),
            PageSize = pageSize,
            Next = next
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> AddGuestHandler(
        string groupId,
        AddGuestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new AddGuestCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            GuestName = request.GuestName
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> GetGroupsWithDetailsHandler(
        int pageSize,
        bool? isArchived,
        string? next,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupsWithDetailsQuery
        {
            UserId = httpContext.GetUserId(),
            IsArchived = isArchived,
            PageSize = pageSize,
            Next = next
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetGroupDetailsHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetGroupDetailsQuery
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAllGroupsBalancesHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetAllGroupsTotalBalancesQuery
        {
            UserId = httpContext.GetUserId(),
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> RemoveMemberHandler(
        string groupId,
        RemoveMemberRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RemoveGroupMemberCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            MemberId = request.MemberId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> RemoveGuestHandler(
        string groupId,
        RemoveGuestRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new RemoveGroupGuestCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            GuestId = request.GuestId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> LeaveGroupHandler(
        string groupId,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new LeaveGroupCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> EditGroupNameHandler(
        string groupId,
        EditGroupNameRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditGroupNameCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            Name = request.Name,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> EditGroupCurrencyHandler(
        string groupId,
        EditGroupCurrencyRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditGroupCurrencyCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            Currency = request.Currency,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }

    private static async Task<IResult> EditGroupArchiveStatusHandler(
        string groupId,
        EditGroupArchiveStatusRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var command = new EditGroupArchiveStatusCommand
        {
            UserId = httpContext.GetUserId(),
            GroupId = groupId,
            IsArchived = request.IsArchived,
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok();
    }
}