using System.Net;
using Reqnroll;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Authentication and the generic authorization-outcome assertions, shared by every feature.</summary>
[Binding]
public sealed class CommonSteps(AuthenticationDriver authentication, HttpResponseContext response)
{
    [Given(@"I am logged in as (customer|warehouse operator|admin) ""(.*)""")]
    [When(@"I am logged in as (customer|warehouse operator|admin) ""(.*)""")]
    public void SignIn(string role, string email) => authentication.SignInAs(email, RoleMap.ToRole(role));

    [Given(@"I am not logged in")]
    public void SignedOut() => authentication.SignOut();

    [Then(@"I am rejected as unauthorized")]
    public void ThenUnauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, response.Require().StatusCode);

    [Then(@"I am rejected as forbidden")]
    public void ThenForbidden() =>
        Assert.Equal(HttpStatusCode.Forbidden, response.Require().StatusCode);
}
