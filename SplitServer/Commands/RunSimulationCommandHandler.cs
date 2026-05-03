using System.Text.Json;
using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.RiskEngine;
using SplitServer.Services.RiskEngine.Models;

namespace SplitServer.Commands;

public class RunSimulationCommandHandler : IRequestHandler<RunSimulationCommand, Result<SimulationResponse>>
{
    private readonly RiskEngineClient _riskEngineClient;
    private readonly IRiskEngineRepository _riskEngineRepository;

    public RunSimulationCommandHandler(RiskEngineClient riskEngineClient, IRiskEngineRepository riskEngineRepository)
    {
        _riskEngineClient = riskEngineClient;
        _riskEngineRepository = riskEngineRepository;
    }

    public async Task<Result<SimulationResponse>> Handle(RunSimulationCommand command, CancellationToken ct)
    {
        var request = new SimulationRequest
        {
            Economy = command.Economy,
            Financials = command.Financials,
            RiskToggles = command.RiskToggles,
            CustomRisks = command.CustomRisks,
            Correlations = command.Correlations
        };

        try
        {
            var response = await _riskEngineClient.RunSimulationAsync(request);

            var existingMaybe = await _riskEngineRepository.GetByUserId(command.UserId, ct);

            var now = DateTime.UtcNow;

            if (existingMaybe.HasValue)
            {
                var existing = existingMaybe.Value;
                if (!SetupMatchesCommand(existing, command))
                {
                    var updated = existing with
                    {
                        Updated = now,
                        Economy = command.Economy,
                        Financials = command.Financials,
                        RiskToggles = command.RiskToggles,
                        CustomRisks = command.CustomRisks,
                        Correlations = command.Correlations
                    };

                    await _riskEngineRepository.UpsertByUserId(updated, ct);
                }
            }
            else
            {
                var setup = new RiskEngineSetup
                {
                    Id = Guid.NewGuid().ToString(),
                    Created = now,
                    Updated = now,
                    UserId = command.UserId,
                    Economy = command.Economy,
                    Financials = command.Financials,
                    RiskToggles = command.RiskToggles,
                    CustomRisks = command.CustomRisks,
                    Correlations = command.Correlations
                };

                await _riskEngineRepository.Insert(setup, ct);
            }

            return Result.Success(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<SimulationResponse>($"Risk engine request failed: {ex.Message}");
        }
    }

    private static bool SetupMatchesCommand(RiskEngineSetup existing, RunSimulationCommand command)
    {
        return existing.Economy == command.Economy
            && JsonSerializer.Serialize(existing.Financials) == JsonSerializer.Serialize(command.Financials)
            && JsonSerializer.Serialize(existing.RiskToggles) == JsonSerializer.Serialize(command.RiskToggles)
            && JsonSerializer.Serialize(existing.CustomRisks) == JsonSerializer.Serialize(command.CustomRisks)
            && JsonSerializer.Serialize(existing.Correlations) == JsonSerializer.Serialize(command.Correlations);
    }
}
