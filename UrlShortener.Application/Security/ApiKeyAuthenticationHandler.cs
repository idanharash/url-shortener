using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace UrlShortener.Application.Security;

/// <summary>
/// Authenticates machine clients by the X-Api-Key header. It is a regular ASP.NET Core authentication scheme,
/// so endpoints only declare [Authorize]; moving to JWT with an identity provider later replaces this scheme's
/// registration, not the controllers.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeySettings settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    private const string ClientName = "api-client";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presentedKey = Request.Headers[HeaderName].ToString();
        if (presentedKey.Length == 0)
            return Task.FromResult(AuthenticateResult.NoResult()); // [Authorize] turns this into a 401

        // Comparing SHA-256 hashes gives fixed-length inputs, so FixedTimeEquals hides the key length too,
        // and every configured key is checked (no early exit) so timing doesn't reveal which one matched.
        var presentedHash = Hash(presentedKey);
        var matched = false;
        foreach (var key in settings.ApiKeys)
            matched |= CryptographicOperations.FixedTimeEquals(presentedHash, Hash(key));

        if (!matched)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, ClientName)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
