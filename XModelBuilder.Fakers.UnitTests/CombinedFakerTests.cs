using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.Bogus;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class CombinedFakerTests
{
    public class Person
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
    }

    private static IModelBuilderProvider CreateProvider(int seed) =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddXFaker(seed)
            .AddBogusFaker(seed)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void XFaker_And_BogusFaker_Coexist_And_BuildDeterministically()
    {
        var p1 = CreateProvider(2024).For<Person>()
            .With("Id", "NewGuid(customer-acme)")     // XFaker, name-based -> stable
            .With("Name", "bogus.name.firstname()")    // BogusFaker, deep-path
            .With("City", "bogus.address.city()")      // BogusFaker, deep-path
            .Build();

        var p2 = CreateProvider(2024).For<Person>()
            .With("Id", "NewGuid(customer-acme)")
            .With("Name", "bogus.name.firstname()")
            .With("City", "bogus.address.city()")
            .Build();

        Assert.Equal(p1.Id, p2.Id);
        Assert.Equal(p1.Name, p2.Name);
        Assert.Equal(p1.City, p2.City);
        Assert.NotEqual(Guid.Empty, p1.Id);
        Assert.False(string.IsNullOrEmpty(p1.Name));
        Assert.False(string.IsNullOrEmpty(p1.City));
    }
}
