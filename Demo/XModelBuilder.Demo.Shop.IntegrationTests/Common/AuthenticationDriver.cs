namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Switches the acting user/role for the scenario (header-based test auth, so no real login).</summary>
public sealed class AuthenticationDriver(CurrentUserContext user)
{
    public void SignInAs(string email, string role)
    {
        user.Email = email;
        user.Role = role;
    }

    public void SignOut()
    {
        user.Email = null;
        user.Role = null;
    }
}
