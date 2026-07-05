using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;

/// <summary>
/// The Customers domain's own scenario context: the <see cref="Customer"/> currently being assembled
/// step by step (first as a person, then extended with addresses in later Gherkin steps).
/// </summary>
public sealed class CustomerBuildContext
{
    public Customer? Current { get; set; }

    public Customer Require() =>
        Current ?? throw new InvalidOperationException("Er is nog geen klant opgebouwd in dit scenario.");
}
