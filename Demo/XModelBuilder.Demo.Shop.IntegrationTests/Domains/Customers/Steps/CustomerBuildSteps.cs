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
    [Given(@"I start building a customer as a person:")]
    public void GivenIStartAsPerson(Table table) =>
        customer.Current = xmodels.For<Customer>("customer").CreateModel(table);

    [When(@"I extend the customer with a shipping address:")]
    [Given(@"I extend the customer with a shipping address:")]
    public void ExtendWithShippingAddress(Table table) => ExtendWithAddress(AddressKind.Shipping, table);

    [When(@"I extend the customer with a billing address:")]
    [Given(@"I extend the customer with a billing address:")]
    public void ExtendWithBillingAddress(Table table) => ExtendWithAddress(AddressKind.Billing, table);

    [Then(@"the customer has (\d+) address(?:es)?")]
    public void ThenCustomerHasAddresses(int count) =>
        Assert.Equal(count, customer.Require().Addresses.Count);

    [Then(@"the customer has a (shipping|billing) address in ""(.*)""")]
    public void ThenCustomerHasAddressInCity(string kindWord, string city)
    {
        var kind = kindWord == "shipping" ? AddressKind.Shipping : AddressKind.Billing;
        Assert.Contains(customer.Require().Addresses, a => a.Kind == kind && a.City == city);
    }

    [Then(@"the customer is named ""(.*)""")]
    public void ThenCustomerIsNamed(string fullName) =>
        Assert.Equal(fullName, customer.Require().FullName);

    [Then(@"the customer has a generated email address")]
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
