using System.Net;
using Reqnroll;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Authentication and the generic authorization-outcome assertions, shared by every feature.</summary>
[Binding]
public sealed class CommonSteps(AuthenticationDriver authentication, HttpResponseContext response)
{
    [Given(@"ik ben ingelogd als (klant|magazijnmedewerker|beheerder) ""(.*)""")]
    [When(@"ik ben ingelogd als (klant|magazijnmedewerker|beheerder) ""(.*)""")]
    public void SignIn(string role, string email) => authentication.SignInAs(email, RoleMap.ToRole(role));

    [Given(@"ik ben niet ingelogd")]
    public void SignedOut() => authentication.SignOut();

    [Then(@"word ik afgewezen als niet-geautoriseerd")]
    public void ThenUnauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, response.Require().StatusCode);

    [Then(@"word ik afgewezen als verboden")]
    public void ThenForbidden() =>
        Assert.Equal(HttpStatusCode.Forbidden, response.Require().StatusCode);
}
