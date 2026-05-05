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

    public async Task<WhatIfResponse> WhatIfAsync(string runId, WhatIfRequest request)
    {
        var response = await _riskEngineHttpClient.PostAsJsonAsync($"/v1/whatif/{runId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WhatIfResponse>()
            ?? throw new InvalidOperationException("Null response from risk engine");
    }

    public async Task<FactorsResponse> GetFactorsAsync(string runId)
    {
        var response = await _riskEngineHttpClient.GetAsync($"/v1/factors/{runId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FactorsResponse>()
            ?? throw new InvalidOperationException("Null response from risk engine");
    }

    public async Task<FairPremiumResponse> GetFairPremiumAsync(string runId, FairPremiumRequest request)
    {
        var response = await _riskEngineHttpClient.PostAsJsonAsync($"/v1/fair-premium/{runId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FairPremiumResponse>()
            ?? throw new InvalidOperationException("Null response from risk engine");
    }

    public async Task<ConditionalQueryResponse> ConditionalProbabilitiesAsync(string runId, ConditionalQueryRequest request)
    {
        var response = await _riskEngineHttpClient.PostAsJsonAsync($"/v1/conditional/{runId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConditionalQueryResponse>()
            ?? throw new InvalidOperationException("Null response from risk engine");
    }
}
