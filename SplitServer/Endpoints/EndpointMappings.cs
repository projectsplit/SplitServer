using SplitServer.Extensions;
using SplitServer.Services.CurrencyExchangeRate;

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
        app.MapGet(
                "/",
                async (HttpContext context, CurrencyExchangeRateService currencyExchangeRateService) =>
                {
                    var asd = new
                    {
                        UserId = context.GetUserId()
                    };

                    var rates = await currencyExchangeRateService.Get(DateOnly.Parse("2025-03-10"), CancellationToken.None);

                    const decimal amount = 10m;

                    return amount.Convert("USD", rates.Value, "EUR");
                })
            .RequireAuthorization();

        return app;
    }
}