namespace SplitServer.Endpoints;

public static class EndpointMappings
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapGroup("/auth").MapAuthEndpoints();
        app.MapGroup("/users").RequireAuthorization().MapUserEndpoints();
        app.MapGroup("/groups").RequireAuthorization().MapGroupEndpoints();
        app.MapGroup("/expenses").RequireAuthorization().MapExpenseEndpoints();
        app.MapGroup("/transfers").RequireAuthorization().MapTransferEndpoints();
        app.MapGroup("/debts").RequireAuthorization().MapDebtEndpoints();
        app.MapGroup("/invitations").RequireAuthorization().MapInvitationEndpoints();
        app.MapGroup("/join").RequireAuthorization().MapJoinEndpoints();
        app.MapGroup("/currency-rates").RequireAuthorization().MapCurrencyExchangeEndpoints();
        app.MapGet("/", (HttpContext context) => throw new Exception("Fake Exception")).RequireAuthorization();

        return app;
    }
}