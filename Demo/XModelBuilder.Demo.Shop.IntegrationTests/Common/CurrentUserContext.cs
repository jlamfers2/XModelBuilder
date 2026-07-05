namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Who is acting in the scenario right now. Set by the <c>AuthenticationDriver</c>.</summary>
public sealed class CurrentUserContext
{
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Email);
}
