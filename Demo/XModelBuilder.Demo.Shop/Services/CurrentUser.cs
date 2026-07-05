using System.Security.Claims;

namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Derives the caller's identity from the authenticated <see cref="ClaimsPrincipal"/>.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
