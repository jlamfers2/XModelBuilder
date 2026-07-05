using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;

/// <summary>
/// Three named builders for the SAME model type (<see cref="Customer"/>), one per role. This is the
/// showcase for <c>[ModelBuilder(name)]</c> + <c>UseAsDefaultModelBuilder</c>: the seed asks for
/// <c>For&lt;Customer&gt;("warehouse")</c> etc. and gets the right role-defaults for free. Filler
/// values come from the seeded <see cref="Faker"/> so they are deterministic.
/// </summary>
[ModelBuilder("customer")]
public sealed class CustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<CustomerBuilder, Customer>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.Customer);
        With(c => c.Email, x => $"customer{x.XFaker().NextId("customer")}@shop.test");
        With(c => c.FullName, x => $"Customer {x.XFaker().NextId("customer-name")}");
    }
}

[ModelBuilder("warehouse")]
public sealed class WarehouseCustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<WarehouseCustomerBuilder, Customer>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.WarehouseOperator);
        With(c => c.Email, x => $"warehouse{x.XFaker().NextId("warehouse")}@shop.test");
        With(c => c.FullName, x => $"Warehouse {x.XFaker().NextId("warehouse-name")}");
    }
}

[ModelBuilder("admin")]
public sealed class AdminCustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<AdminCustomerBuilder, Customer>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.Admin);
        With(c => c.Email, x => $"admin{x.XFaker().NextId("admin")}@shop.test");
        With(c => c.FullName, x => $"Admin {x.XFaker().NextId("admin-name")}");
    }
}
