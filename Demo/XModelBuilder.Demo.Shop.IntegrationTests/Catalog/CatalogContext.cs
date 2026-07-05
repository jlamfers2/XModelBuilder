using XModelBuilder.Demo.Shop.Contracts;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Catalog;

/// <summary>The last product created by the scenario. The Catalog domain's own scenario context.</summary>
public sealed class CatalogContext
{
    public ProductResponse? LastCreated { get; set; }
}
