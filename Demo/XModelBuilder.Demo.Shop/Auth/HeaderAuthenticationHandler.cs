using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace XModelBuilder.Demo.Shop.Auth;

/// <summary>
/// A deliberately simple, demo-only authentication scheme: it trusts two request headers,
/// <c>X-User-Email</c> and <c>X-User-Role</c>, and turns them into a <see cref="ClaimsPrincipal"/>.
/// It stands in for a real identity provider (JWT/OpenID) so the demo stays self-contained; the
/// integration tests replace it entirely with their own test scheme.
/// </summary>
public sealed class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Header";
    public const string EmailHeader = "X-User-Email";
    public const string RoleHeader = "X-User-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(EmailHeader, out var email) || string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers.TryGetValue(RoleHeader, out var r) ? r.ToString() : string.Empty;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email!),
            new(ClaimTypes.Email, email!),
        };
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
