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
}