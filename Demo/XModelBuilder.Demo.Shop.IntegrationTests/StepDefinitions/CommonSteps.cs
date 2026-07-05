using System.Net;
using Reqnroll;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Demo.Shop.IntegrationTests.Contexts;
using XModelBuilder.Demo.Shop.IntegrationTests.Drivers;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.StepDefinitions;

/// <summary>Maps the Dutch role words used in scenarios to the application's role names.</summary>
internal static class RoleMap
{
    public static string ToRole(string dutch) => dutch switch
    {
        "klant" => Roles.Customer,
        "magazijnmedewerker" => Roles.WarehouseOperator,
        "beheerder" => Roles.Admin,
        _ => throw new ArgumentException($"Onbekende rol '{dutch}'."),
    };
}

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
