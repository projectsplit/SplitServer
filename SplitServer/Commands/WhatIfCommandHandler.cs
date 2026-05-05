using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;

namespace SplitServer.Commands;

public class WhatIfCommandHandler : IRequestHandler<WhatIfCommand, Result<WhatIfResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly ICalculatedWealthRepository _calculatedWealthRepository;

    public WhatIfCommandHandler(
        RiskEngineClient riskEngineClient,
        ICalculatedWealthRepository calculatedWealthRepository)
    {
        _riskEngineClient = riskEngineClient;
        _calculatedWealthRepository = calculatedWealthRepository;
    }

    public async Task<Result<WhatIfResponse>> Handle(WhatIfCommand command, CancellationToken ct)
    {
        var wealthMaybe = await _calculatedWealthRepository.GetByUserId(command.UserId, ct);

        if (wealthMaybe.HasNoValue)
            return Result.Failure<WhatIfResponse>("No simulation found. Run a simulation first.");

        var runId = wealthMaybe.Value.RunId;

        var request = new WhatIfRequest
        {
            BufferDelta = command.BufferDelta,
            ExpenseCut = command.ExpenseCut,
            SalaryDelta = command.SalaryDelta,
            Reweight = command.Reweight,
            DisabledRisks = command.DisabledRisks,
            RiskCaps = command.RiskCaps,
            ExcludeProperty = command.ExcludeProperty
        };

        try
        {
            var response = await _riskEngineClient.WhatIfAsync(runId, request);
            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<WhatIfResponse>($"Risk engine request failed: {ex.Message}");
        }
    }
}
