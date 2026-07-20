using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class WithDefaultTests
{
    // ---- Models ---------------------------------------------------------------------------------

    public class Person
    {
        public string? Name { get; set; }
        public Address? Address { get; set; }
    }

    public class Address
    {
        public string? Street { get; set; }
        public string? City { get; set; }
    }

    public class PersonWithCtorAddress(Address address)
    {
        public Address Address { get; } = address;
    }

    // Three levels to show a graph filling itself one explicit level per builder.
    public class Level1 { public Level2? Child { get; set; } }
    public class Level2 { public Level3? Child { get; set; } }
    public class Level3 { public string? Value { get; set; } }

    // ---- Builders -------------------------------------------------------------------------------

    [ModelBuilder("defaultAddress")]
    public sealed class AddressBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<AddressBuilder, Address>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(a => a.City, "DefaultCity");
            With(a => a.Street, "DefaultStreet");
        }
    }

    [ModelBuilder("person")]
    public sealed class PersonBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<PersonBuilder, Person>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(p => p.Address);
    }

    [ModelBuilder("level1")]
    public sealed class Level1Builder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<Level1Builder, Level1>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(x => x.Child);
    }

    [ModelBuilder("level2")]
    public sealed class Level2Builder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<Level2Builder, Level2>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(x => x.Child);
    }

    [ModelBuilder("level3")]
    public sealed class Level3Builder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<Level3Builder, Level3>(options, xprovider)
    {
        protected override void SetDefaults() => With(x => x.Value, "deep");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static IModelBuilderProvider Provider(params Type[] builderTypes)
    {
        var services = new ServiceCollection().AddXModelBuilder();
        foreach (var builderType in builderTypes)
        {
            services.AddModelBuilder(builderType);
        }
        return services.BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    }

    // ---- Tests ----------------------------------------------------------------------------------

    [Fact]
    public void WithDefault_In_SetDefaults_Fills_Nested_Model_Via_Its_Default_Builder()
    {
        // Arrange
        var xprovider = Provider(typeof(PersonBuilder), typeof(AddressBuilder));

        // Act
        var person = xprovider.For<Person>().Build();

        // Assert
        Assert.NotNull(person.Address);
        Assert.Equal("DefaultCity", person.Address!.City);
        Assert.Equal("DefaultStreet", person.Address.Street);
    }

    [Fact]
    public void WithDefault_Works_Ad_Hoc_In_The_Fluent_Chain()
    {
        // Arrange
        var xprovider = Provider(typeof(AddressBuilder)); // no PersonBuilder registered

        // Act
        var person = xprovider.For<Person>()
            .WithDefault(p => p.Address)
            .With(p => p.Name, "Jane")
            .Build();

        // Assert
        Assert.Equal("Jane", person.Name);
        Assert.NotNull(person.Address);
        Assert.Equal("DefaultCity", person.Address!.City);
    }

    [Fact]
    public void WithDefault_Is_Equivalent_To_The_Default_Token_String()
    {
        // Arrange
        var xprovider = Provider(typeof(AddressBuilder));

        // Act
        var viaTyped = xprovider.For<Person>().WithDefault(p => p.Address).Build();
        var viaToken = xprovider.For<Person>().With("Address", "default()").Build();

        // Assert
        Assert.Equal(viaToken.Address!.City, viaTyped.Address!.City);
        Assert.Equal(viaToken.Address.Street, viaTyped.Address.Street);
    }

    [Fact]
    public void WithDefault_On_String_Member_Yields_Null_Matching_The_Default_Token()
    {
        // Arrange
        var xprovider = Provider();

        // Act
        var person = xprovider.For<Person>()
            .With(p => p.Name, "initial")
            .WithDefault(p => p.Name) // string -> null, like "default()"
            .Build();

        // Assert
        Assert.Null(person.Name);
    }

    [Fact]
    public void WithDefault_Is_Routed_Into_A_Constructor_Argument()
    {
        // Arrange
        var xprovider = Provider(typeof(AddressBuilder));

        // Act
        var person = xprovider.For<PersonWithCtorAddress>()
            .WithDefault(p => p.Address)
            .Build();

        // Assert
        Assert.NotNull(person.Address);
        Assert.Equal("DefaultCity", person.Address.City);
    }

    [Fact]
    public void WithDefault_Composes_A_Deep_Graph_One_Level_Per_Builder()
    {
        // Arrange
        var xprovider = Provider(typeof(Level1Builder), typeof(Level2Builder), typeof(Level3Builder));

        // Act
        var level1 = xprovider.For<Level1>().Build();

        // Assert
        Assert.NotNull(level1.Child);
        Assert.NotNull(level1.Child!.Child);
        Assert.Equal("deep", level1.Child.Child!.Value);
    }
}
