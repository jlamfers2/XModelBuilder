using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Catalog;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>
/// The optional AGGREGATE driver: composes the per-domain drivers, for steps that orchestrate several
/// domains in one flow (e.g. authenticate → place → pay → ship). Steps that touch a single domain inject
/// that domain's driver directly instead.
/// </summary>
public sealed class ShopDriver(AuthenticationDriver authentication, OrderApiDriver orders, CatalogApiDriver catalog)
{
    public AuthenticationDriver Authentication => authentication;
    public OrderApiDriver Orders => orders;
    public CatalogApiDriver Catalog => catalog;
}
