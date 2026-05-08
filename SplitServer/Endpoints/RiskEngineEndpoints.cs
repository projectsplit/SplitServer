using MediatR;
using SplitServer.Commands;
using SplitServer.Extensions;
using SplitServer.Queries;

namespace SplitServer.Endpoints;

public static class RiskEngineEndpoints
{
    public static void MapRiskEngineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/simulate", RunSimulationHandler);
        app.MapGet("/mostrecentsetup", GetMostRecentEngineSetupHandler);
        app.MapGet("/calculatedwealth", GetCalculatedWealthHandler);
        app.MapPost("/whatif", WhatIfHandler);
        app.MapGet("/factors", GetFactorsHandler);
        app.MapPost("/fairpremium", GetFairPremiumHandler);
        app.MapPost("/conditional", GetConditionalProbsHandler);
        app.MapPost("/conditionalsweep", GetConditionalSweepProbsHandler);
        app.MapPost("/taildrivers", GetTailDriversHandler);
    }

    private static async Task<IResult> RunSimulationHandler(
        IMediator mediator,
        HttpContext httpContext,
        SimulationRequest request,
        CancellationToken ct)
    {
        var command = new RunSimulationCommand
        {
            UserId = httpContext.GetUserId(),
            Economy = request.Economy,
            Financials = request.Financials,
            RiskToggles = request.RiskToggles,
            CustomRisks = request.CustomRisks,
            Correlations = request.Correlations
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetMostRecentEngineSetupHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetMostRecentEngineSetupQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetCalculatedWealthHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetCalculatedWealthQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> WhatIfHandler(
        IMediator mediator,
        HttpContext httpContext,
        WhatIfRequest request,
        CancellationToken ct)
    {
        var command = new WhatIfCommand
        {
            UserId = httpContext.GetUserId(),
            BufferDelta = request.BufferDelta,
            ExpenseCut = request.ExpenseCut,
            SalaryDelta = request.SalaryDelta,
            Reweight = request.Reweight,
            DisabledRisks = request.DisabledRisks,
            RiskCaps = request.RiskCaps,
            ExcludeProperty = request.ExcludeProperty
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetFactorsHandler(
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var query = new GetFactorsQuery
        {
            UserId = httpContext.GetUserId()
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetFairPremiumHandler(
        IMediator mediator,
        HttpContext httpContext,
        FairPremiumRequest request,
        CancellationToken ct)
    {
        var query = new GetFairPremiumQuery
        {
            UserId = httpContext.GetUserId(),
            RiskName = request.RiskName,
            MaxLoss = request.MaxLoss
        };

        var result = await mediator.Send(query, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetConditionalProbsHandler(
        IMediator mediator,
        HttpContext httpContext,
        ConditionalQueryRequest request,
        CancellationToken ct)
    {
        var command = new ConditionalProbabilitiesCommand
        {
            UserId = httpContext.GetUserId(),
            Conditions = request.Conditions
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetConditionalSweepProbsHandler(
        IMediator mediator,
        HttpContext httpContext,
        ConditionalSweepRequest request,
        CancellationToken ct)
    {
        var command = new ConditionalSweepCommand
        {
            UserId = httpContext.GetUserId(),
            Factor = request.Factor,
            Op = request.Op,
            Thresholds = request.Thresholds,
            AutoQuantiles = request.AutoQuantiles
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetTailDriversHandler(
        IMediator mediator,
        HttpContext httpContext,
        TailDriversRequest request,
        CancellationToken ct)
    {
        var command = new TailDriversCommand
        {
            UserId = httpContext.GetUserId(),
            ExcludeProperty = request.ExcludeProperty,
            TailThresholdBusts = request.TailThresholdBusts,
            TailFallbackPct = request.TailFallbackPct,
            PairQuantile = request.PairQuantile,
            PairTopN = request.PairTopN,
            PathDepth = request.PathDepth,
            PathTopN = request.PathTopN
        };

        var result = await mediator.Send(command, ct);

        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
    }
}
