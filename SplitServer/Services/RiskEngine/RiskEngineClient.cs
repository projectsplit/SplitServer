using System.Net.Http.Headers;
using System.Net.Mime;

namespace SplitServer.Services.RiskEngine;

public class RiskEngineClient
{
    private readonly HttpClient _riskEngineHttpClient;

    public RiskEngineClient(
        IHttpClientFactory httpClientFactory
    )
    {
        _riskEngineHttpClient = httpClientFactory.CreateClient();
        _riskEngineHttpClient.BaseAddress = new Uri("http://localhost:8000");
        _riskEngineHttpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json)
        );
    }

    public async Task<SimulationResponse> RunSimulationAsync(SimulationRequest request)
    {
        var response = await _riskEngineHttpClient.PostAsJsonAsync("/v1/simulate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SimulationResponse>()
            ?? throw new InvalidOperationException("Null response from risk engine");
    }
}
