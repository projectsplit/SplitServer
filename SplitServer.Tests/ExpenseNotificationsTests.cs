using SplitServer.Services;

namespace SplitServer.Tests;

public class ExpenseNotificationsTests
{
    [Fact]
    public void A_described_expense_is_quoted_with_its_amount_after_it()
    {
        Assert.Equal("\"Dinner\" (42.5 EUR)", ExpenseNotifications.Describe("Dinner", 42.5m, "EUR"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_expense_without_a_description_is_just_its_amount(string? description)
    {
        Assert.Equal("20 EUR", ExpenseNotifications.Describe(description, 20m, "EUR"));
    }
}
