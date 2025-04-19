namespace SplitServer.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ready", () => Results.Ok());
        app.MapGet("/error", _ => throw new Exception("Fake Exception"));
    }
}