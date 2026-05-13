using SplitServer.Services;
using Xunit.Abstractions;

namespace SplitServer.Tests;

public class ValidationServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ValidationServiceTests(
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_username")]
    [InlineData(".username")]
    [InlineData("username.")]
    [InlineData("username_")]
    [InlineData("user__name")]
    [InlineData("user..name")]
    [InlineData("user_.name")]
    [InlineData("user._name")]
    [InlineData("use")]
    [InlineData("username17username")]
    public void ValidateUsername_ShouldReturnFailure_WhenUsernameIsInvalid(string username)
    {
        var validationService = new ValidationService();

        var result = validationService.ValidateUsername(username);

        Assert.True(result.IsFailure);

        _testOutputHelper.WriteLine(result.Error);
    }

    [Theory]
    [InlineData("username")]
    [InlineData("username123")]
    [InlineData("123username")]
    [InlineData("username_123")]
    [InlineData("1234.username")]
    [InlineData("1234_username")]
    [InlineData("username.user")]
    [InlineData("username_user")]
    [InlineData("user.user_name")]
    [InlineData("user_user.name")]
    [InlineData("user.user.name")]
    [InlineData("user_user_name")]
    [InlineData("2345.user")]
    [InlineData("3456_user")]
    [InlineData("234.user_23525")]
    [InlineData("64564_4565.name")]
    [InlineData("user.456745.name")]
    [InlineData("user_456474_7686")]
    public void ValidateUsername_ShouldReturnSuccess_WhenUsernameIsValid(string username)
    {
        var validationService = new ValidationService();

        var result = validationService.ValidateUsername(username);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("@no-local.com")]
    [InlineData("no-at-sign.com")]
    [InlineData("no-tld@example")]
    [InlineData("spaces in@email.com")]
    [InlineData("trailing-space@email.com ")]
    [InlineData("a@b@c.com")]
    public void ValidateEmail_ShouldReturnFailure_WhenEmailIsInvalid(string email)
    {
        var validationService = new ValidationService();

        var result = validationService.ValidateEmail(email);

        Assert.True(result.IsFailure);

        _testOutputHelper.WriteLine(result.Error);
    }

    [Fact]
    public void ValidateEmail_ShouldReturnFailure_WhenEmailExceedsMaxLength()
    {
        var validationService = new ValidationService();

        var localPart = new string('a', 250);
        var email = $"{localPart}@example.com";

        var result = validationService.ValidateEmail(email);

        Assert.True(result.IsFailure);

        _testOutputHelper.WriteLine(result.Error);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user+tag@example.co.uk")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("123@example.io")]
    [InlineData("a@b.cd")]
    public void ValidateEmail_ShouldReturnSuccess_WhenEmailIsValid(string email)
    {
        var validationService = new ValidationService();

        var result = validationService.ValidateEmail(email);

        Assert.True(result.IsSuccess);
    }
}