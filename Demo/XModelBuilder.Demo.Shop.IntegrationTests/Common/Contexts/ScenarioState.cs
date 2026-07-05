using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Catalog;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>
/// The optional AGGREGATE context: bundles the per-domain scenario contexts so a step (or driver) that
/// needs several of them can take just this one. Steps that touch a single domain inject only that
/// domain's context (<see cref="OrderContext"/>, <see cref="CatalogContext"/>,
/// <see cref="CustomerBuildContext"/>, ...) directly.
/// </summary>
public sealed class ScenarioState(
    CurrentUserContext user,
    HttpResponseContext response,
    OrderContext order,
    CatalogContext catalog,
    CustomerBuildContext customer)
{
    public CurrentUserContext User => user;
    public HttpResponseContext Response => response;
    public OrderContext Order => order;
    public CatalogContext Catalog => catalog;
    public CustomerBuildContext Customer => customer;
}
