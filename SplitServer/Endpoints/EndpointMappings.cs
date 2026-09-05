namespace SplitServer.Endpoints;

public static class EndpointMappings
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapGroup("/auth").MapAuthEndpoints();
        app.MapGroup("/users").RequireAuthorization().MapUserEndpoints();
        app.MapGroup("/groups").RequireAuthorization().MapGroupEndpoints();
        app.MapGroup("/expenses").RequireAuthorization().MapExpenseEndpoints();
        app.MapGroup("/recurring-expenses").RequireAuthorization().MapRecurringExpenseEndpoints();
        app.MapGroup("/transfers").RequireAuthorization().MapTransferEndpoints();
        app.MapGroup("/debts").RequireAuthorization().MapDebtEndpoints();
        app.MapGroup("/invitations").RequireAuthorization().MapInvitationEndpoints();
        app.MapGroup("/join").RequireAuthorization().MapJoinEndpoints();
        app.MapGroup("/currency-rates").RequireAuthorization().MapCurrencyExchangeEndpoints();
        app.MapGroup("/analytics").RequireAuthorization().MapAnalyticsEndpoints();
        app.MapGroup("/budgets").RequireAuthorization().MapBudgetsEndpoints();
        app.MapGroup("/notifications").RequireAuthorization().MapNotificationEndpoints();
        app.MapGroup("/connections").RequireAuthorization().MapConnectionEndpoints();
        app.MapGroup("/donations").RequireAuthorization().MapDonationEndpoints();

        // Anonymous by design: the caller is Google Cloud Pub/Sub relaying Play, not a signed-in
        // app. A shared secret on the URL stands in for a token, and every purchase named in a
        // notification is re-read from the Play API before anything is written.
        app.MapGroup("/google-play").MapPlayNotificationEndpoints();

        app.MapGroup("/health").MapHealthEndpoints();

        return app;
    }
}