using System.Security.Claims;

namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Ambient information about the caller, derived from the authenticated principal.</summary>
public interface ICurrentUser
{
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
