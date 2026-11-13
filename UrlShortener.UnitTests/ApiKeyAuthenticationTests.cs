using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Application.Security;

namespace UrlShortener.UnitTests;

public class ApiKeyAuthenticationTests
{
    private const string CurrentKey = "current-key-0123456789abcdef0123456789";
    private const string RotatedKey = "rotated-key-0123456789abcdef0123456789";

    [Fact]
    public async Task MissingHeader_GivesNoResult_SoTheEndpointChallengesWith401()
    {
        var result = await AuthenticateAsync(presentedKey: null);

        Assert.True(result.None);
    }

    [Theory]
    [InlineData("wrong-key-0123456789abcdef0123456789xx")]
    [InlineData("current-key")] // prefix of a valid key
    public async Task WrongKey_Fails(string presentedKey)
    {
        var result = await AuthenticateAsync(presentedKey);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Theory]
    [InlineData(CurrentKey)]
    [InlineData(RotatedKey)] // several keys are valid at once, which allows rotation without downtime
    public async Task ConfiguredKey_Succeeds(string presentedKey)
    {
        var result = await AuthenticateAsync(presentedKey);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Settings_RejectAnEmptyKeyList()
    {
        var settings = new ApiKeySettings();

        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), [], validateAllProperties: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData(CurrentKey, "too-short")] // one bad key is enough to refuse startup
    public void Settings_RejectMissingOrShortKeys(params string[] keys)
    {
        var settings = new ApiKeySettings { ApiKeys = keys };

        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), [], validateAllProperties: true));
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(string? presentedKey)
    {
        var options = Mock.Of<IOptionsMonitor<AuthenticationSchemeOptions>>(
            monitor => monitor.Get(It.IsAny<string>()) == new AuthenticationSchemeOptions());
        var handler = new ApiKeyAuthenticationHandler(
            options, NullLoggerFactory.Instance, UrlEncoder.Default,
            new ApiKeySettings { ApiKeys = [CurrentKey, RotatedKey] });

        var context = new DefaultHttpContext();
        if (presentedKey is not null)
            context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = presentedKey;

        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }
}
