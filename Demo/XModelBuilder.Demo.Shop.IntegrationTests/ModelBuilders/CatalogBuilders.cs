using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;

/// <summary>Builder for catalog <see cref="Product"/>s used by the seed (category is set by the seeder).</summary>
[ModelBuilder("product")]
public sealed class ProductBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<ProductBuilder, Product>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(p => p.Sku, x => x.XFaker().Sequence("SKU-{0:0000}"));
        With(p => p.Name, "Sample product");
        With(p => p.UnitPrice, 9.99m);
        With(p => p.StockQuantity, 25);
    }
}
