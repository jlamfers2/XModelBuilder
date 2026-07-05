namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Ambient information about the caller, derived from the authenticated principal.</summary>
public interface ICurrentUser
{
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
