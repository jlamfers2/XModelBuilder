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

    // Three levels to show a graph filling itself one explicit level per default-layer entry.
    public class Level1 { public Level2? Child { get; set; } }
    public class Level2 { public Level3? Child { get; set; } }
    public class Level3 { public string? Value { get; set; } }

    // ---- Cross-cutting layer --------------------------------------------------------------------------

    // WithDefault / the "default()" token build a nested value through For(type) with NO name, which
    // resolves to the DEFAULT LAYER for that type (chapter 5). So the nested defaults live here.
    public sealed class GraphDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GraphDefaults<TModel>, TModel>(options, xprovider)
        where TModel : class
    {
        protected override void SetDefaults()
        {
            var t = typeof(TModel);
            if (t == typeof(Address))
            {
                With("City", "DefaultCity");
                With("Street", "DefaultStreet");
            }
            else if (t == typeof(Level1) || t == typeof(Level2))
            {
                With("Child", "default()");
            }
            else if (t == typeof(Level3))
            {
                With("Value", "deep");
            }
        }
    }

    // ---- Builders -------------------------------------------------------------------------------

    [ModelBuilder("person")]
    public sealed class PersonBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<PersonBuilder, Person>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(p => p.Address);
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static IModelBuilderProvider Provider(params Type[] builderTypes)
    {
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(GraphDefaults<>));
        foreach (var builderType in builderTypes)
        {
            services.AddModelBuilder(builderType);
        }
        return services.BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    }

    // ---- Tests ----------------------------------------------------------------------------------

    [Fact]
    public void WithDefault_In_SetDefaults_Fills_Nested_Model_Via_The_Default_Layer()
    {
        // Arrange
        var xprovider = Provider(typeof(PersonBuilder));

        // Act
        var person = xprovider.Use<PersonBuilder>().Build();

        // Assert
        Assert.NotNull(person.Address);
        Assert.Equal("DefaultCity", person.Address!.City);
        Assert.Equal("DefaultStreet", person.Address.Street);
    }

    [Fact]
    public void WithDefault_Works_Ad_Hoc_In_The_Fluent_Chain()
    {
        // Arrange
        var xprovider = Provider(); // no PersonBuilder; the nested Address comes from the cross-cutting layer

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
        var xprovider = Provider();

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
        var xprovider = Provider();

        // Act
        var person = xprovider.For<PersonWithCtorAddress>()
            .WithDefault(p => p.Address)
            .Build();

        // Assert
        Assert.NotNull(person.Address);
        Assert.Equal("DefaultCity", person.Address.City);
    }

    [Fact]
    public void WithDefault_Composes_A_Deep_Graph_One_Level_Per_Default_Layer_Entry()
    {
        // Arrange
        var xprovider = Provider();

        // Act
        var level1 = xprovider.For<Level1>().Build();

        // Assert
        Assert.NotNull(level1.Child);
        Assert.NotNull(level1.Child!.Child);
        Assert.Equal("deep", level1.Child.Child!.Value);
    }
}
