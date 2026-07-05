using Reqnroll;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Reqnroll;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;

/// <summary>
/// The AGGREGATED-BUILD showcase: a <see cref="Customer"/> is composed across several Gherkin steps.
/// First it is built as a person (name/email), then each following step EXTENDS that same instance with
/// an address - without re-running the customer builder's defaults - using
/// <c>NewDefaultModelBuilder&lt;Customer&gt;().With(...).Extend(existing)</c>. The addresses themselves
/// are built from their own compact tables via the "customerAddress" builder (Bogus-backed defaults).
/// </summary>
[Binding]
public sealed class CustomerBuildSteps(IModelBuilderProvider xmodels, CustomerBuildContext customer)
{
    [Given(@"ik bouw een klant op als persoon:")]
    public void GivenIStartAsPerson(Table table) =>
        customer.Current = xmodels.For<Customer>("customer").CreateModel(table);

    [When(@"ik de klant uitbreid met een verzendadres:")]
    [Given(@"ik de klant uitbreid met een verzendadres:")]
    public void ExtendWithShippingAddress(Table table) => ExtendWithAddress(AddressKind.Shipping, table);

    [When(@"ik de klant uitbreid met een factuuradres:")]
    [Given(@"ik de klant uitbreid met een factuuradres:")]
    public void ExtendWithBillingAddress(Table table) => ExtendWithAddress(AddressKind.Billing, table);

    [Then(@"heeft de klant (\d+) adres(?:sen)?")]
    public void ThenCustomerHasAddresses(int count) =>
        Assert.Equal(count, customer.Require().Addresses.Count);

    [Then(@"heeft de klant een (verzend|factuur)adres in ""(.*)""")]
    public void ThenCustomerHasAddressInCity(string kindWord, string city)
    {
        var kind = kindWord == "verzend" ? AddressKind.Shipping : AddressKind.Billing;
        Assert.Contains(customer.Require().Addresses, a => a.Kind == kind && a.City == city);
    }

    [Then(@"heet de klant ""(.*)""")]
    public void ThenCustomerIsNamed(string fullName) =>
        Assert.Equal(fullName, customer.Require().FullName);

    [Then(@"heeft de klant een gegenereerd e-mailadres")]
    public void ThenCustomerHasGeneratedEmail() =>
        Assert.Contains("@", customer.Require().Email);

    /// <summary>
    /// Builds one <see cref="Address"/> from its table (Bogus fills the unspecified fields), then extends
    /// the person built earlier by appending it - the actual XModelBuilder <c>Extend</c> capability.
    /// </summary>
    private void ExtendWithAddress(AddressKind kind, Table table)
    {
        var address = xmodels.For<Address>("customerAddress")
            .With(a => a.Kind, kind)
            .CreateModel(table);

        customer.Current = xmodels.NewDefaultModelBuilder<Customer>()
            .With(c => c.Addresses, [.. customer.Require().Addresses, address])
            .Extend(customer.Require());
    }
}
