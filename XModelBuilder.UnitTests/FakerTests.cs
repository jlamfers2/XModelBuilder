using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class FakerTests
{
    public class Widget
    {
        public string Name { get; set; } = null!;
        public DateTime Birthday { get; set; }
    }

    public class PersonFakers : IFaker
    {
        public DateTime AgeBetween(int minYears, int maxYears) => DateTime.Today.AddYears(-minYears);

        public string RandomString() => "random-string";

        public string RandomString(int length) => new string('x', length);

        public object Fixture(Type type) =>
            type == typeof(string) ? "fixture-string" : Activator.CreateInstance(type)!;
    }

    public class OtherFakers : IFaker
    {
        public string RandomString() => "other-random-string";
    }

    public class MarkerService
    {
        public string Value => "marker-value";
    }

    public class Pet
    {
        public Pet(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class CounterFakers : IFaker
    {
        private int _counter;

        public string NextName() => $"Name{_counter++}";
    }

    public class AdvancedFakers : IFaker
    {
        protected string Secret() => "secret-value";

        private string Hidden() => "hidden-value";

        public T Create<T>() where T : new() => new();

        public string FromServices(IServiceProvider services) => services.GetRequiredService<MarkerService>().Value;

        public string FromServicesWithType(Type type, IServiceProvider services) =>
            $"{type.Name}:{services.GetRequiredService<MarkerService>().Value}";

        public string FromServicesReversedOrder(IServiceProvider services, Type type) =>
            $"{type.Name}:{services.GetRequiredService<MarkerService>().Value}";

        public static string StaticValue() => "static-value";

        public static string StaticValueWithArg(int n) => $"static-{n}";

        protected static string ProtectedStaticValue() => "protected-static-value";

        private static string PrivateStaticValue() => "private-static-value";
    }

    private static IModelBuilderProvider CreateProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection().AddXModelBuilder();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    }

    [Fact]
    public void Token_Invokes_Faker_Method_With_Arguments()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Birthday", "AgeBetween(1,20)").Build();

        // Assert
        Assert.Equal(DateTime.Today.AddYears(-1), widget.Birthday);
    }

    [Fact]
    public void Token_Resolves_Overload_By_Argument_Count()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act
        var noArgs = xprovider.For<Widget>().With("Name", "RandomString()").Build();
        var withArg = xprovider.For<Widget>().With("Name", "RandomString(5)").Build();

        // Assert
        Assert.Equal("random-string", noArgs.Name);
        Assert.Equal("xxxxx", withArg.Name);
    }

    [Fact]
    public void Token_TypeFirstParameter_Is_AutoInjected_And_Not_Counted_As_Argument()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "Fixture()").Build();

        // Assert
        Assert.Equal("fixture-string", widget.Name);
    }

    [Fact]
    public void Token_LastRegisteredFakerClass_Wins_On_Name_Collision()
    {
        // Arrange
        var xprovider = CreateProvider(s => s
            .AddFaker<PersonFakers>()
            .AddFaker<OtherFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "RandomString()").Build();

        // Assert
        Assert.Equal("other-random-string", widget.Name);
    }

    [Fact]
    public void Token_UnknownFakerName_Throws()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            xprovider.For<Widget>().With("Name", "DoesNotExist()").Build());
    }

    [Fact]
    public void Token_NoMatchingOverload_Throws()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act & Assert
        Assert.Throws<MissingMethodException>(() =>
            xprovider.For<Widget>().With("Name", "RandomString(1,2,3)").Build());
    }

    [Fact]
    public void Escaped_FakerLookingText_IsTreatedAsLiteral()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "@RandomString()").Build();

        // Assert
        Assert.Equal("RandomString()", widget.Name);
    }

    [Fact]
    public void Token_Invokes_Protected_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "Secret()").Build();

        // Assert
        Assert.Equal("secret-value", widget.Name);
    }

    [Fact]
    public void Token_Does_Not_Find_Private_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            xprovider.For<Widget>().With("Name", "Hidden()").Build());
    }

    [Fact]
    public void Token_Invokes_Static_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "StaticValue()").Build();

        // Assert
        Assert.Equal("static-value", widget.Name);
    }

    [Fact]
    public void Token_Invokes_Static_Faker_Method_With_Arguments()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "StaticValueWithArg(7)").Build();

        // Assert
        Assert.Equal("static-7", widget.Name);
    }

    [Fact]
    public void Token_Invokes_Protected_Static_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "ProtectedStaticValue()").Build();

        // Assert
        Assert.Equal("protected-static-value", widget.Name);
    }

    [Fact]
    public void Token_Does_Not_Find_Private_Static_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            xprovider.For<Widget>().With("Name", "PrivateStaticValue()").Build());
    }

    [Fact]
    public void Token_Does_Not_Find_Generic_Faker_Method()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            xprovider.For<Widget>().With("Name", "Create()").Build());
    }

    [Fact]
    public void Generic_Faker_Method_Is_Callable_Via_Typed_Faker()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<AdvancedFakers>());

        // Act
        var widget = xprovider.Faker<AdvancedFakers>().Create<Widget>();

        // Assert
        Assert.NotNull(widget);
    }

    [Fact]
    public void Token_InjectsIServiceProvider_AsLeadingParameter()
    {
        // Arrange
        var xprovider = CreateProvider(s => s
            .AddFaker<AdvancedFakers>()
            .AddSingleton<MarkerService>());

        // Act
        var widget = xprovider.For<Widget>().With("Name", "FromServices()").Build();

        // Assert
        Assert.Equal("marker-value", widget.Name);
    }

    [Fact]
    public void Token_InjectsTypeAndIServiceProvider_RegardlessOfOrder()
    {
        // Arrange
        var xprovider = CreateProvider(s => s
            .AddFaker<AdvancedFakers>()
            .AddSingleton<MarkerService>());

        // Act
        var withTypeFirst = xprovider.For<Widget>().With("Name", "FromServicesWithType()").Build();
        var withServicesFirst = xprovider.For<Widget>().With("Name", "FromServicesReversedOrder()").Build();

        // Assert
        Assert.Equal("String:marker-value", withTypeFirst.Name);
        Assert.Equal("String:marker-value", withServicesFirst.Name);
    }

    [Fact]
    public void Faker_ResolvesTypedFakerDirectly_OnDIProvider()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<PersonFakers>());

        // Act
        var faker = xprovider.Faker<PersonFakers>();

        // Assert
        Assert.Equal("random-string", faker.RandomString());
    }

    [Fact]
    public void Faker_SameInstance_AsConstructorInjectedConcreteType()
    {
        // Arrange
        var services = new ServiceCollection().AddXModelBuilder().AddFaker<PersonFakers>();
        var sp = services.BuildServiceProvider();
        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var viaFaker = xprovider.Faker<PersonFakers>();
        var viaConstructorInjection = sp.GetRequiredService<PersonFakers>();

        // Assert
        Assert.Same(viaConstructorInjection, viaFaker);
    }

    [Fact]
    public void Faker_UnregisteredType_Throws()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => xprovider.Faker<PersonFakers>());
    }

    [Fact]
    public void Token_OnNonConformingProvider_ThrowsNotSupported()
    {
        // Arrange
        // Bind the builder's own _xprovider directly to a provider that does NOT implement the
        // internal faker-invocation interface - going through .For<TModel>() would bind it to
        // the REAL, wrapped provider instead (since that's what actually constructs the builder).
        var plainProvider = new PlainModelBuilderProvider(CreateProvider(s => s.AddFaker<PersonFakers>()));
        var builder = new XModelBuilder.Default.DefaultModelBuilder<Widget>(
            Microsoft.Extensions.Options.Options.Create(new ModelBuilderOptions()), plainProvider);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            builder.With("Name", "RandomString()").Build());
    }

    [Fact]
    public void BuildMany_ReevaluatesCtorBoundFakerToken_PerBuild()
    {
        // Arrange
        var xprovider = CreateProvider(s => s.AddFaker<CounterFakers>());

        // Act
        var pets = xprovider.For<Pet>().With("Name", "NextName()").BuildMany(3);

        // Assert
        Assert.Equal("Name0", pets[0].Name);
        Assert.Equal("Name1", pets[1].Name);
        Assert.Equal("Name2", pets[2].Name);
    }

    // Minimal IModelBuilderProvider wrapper that deliberately does NOT implement the internal
    // faker-invocation interface, to prove the graceful-degradation fallback in ValueConverter.
    private sealed class PlainModelBuilderProvider(IModelBuilderProvider inner) : IModelBuilderProvider
    {
        public IModelBuilder For(Type modelType) => inner.For(modelType);
        public IModelBuilder<TModel> For<TModel>() where TModel : class => inner.For<TModel>();
        public IModelBuilder For(Type modelType, string name) => inner.For(modelType, name);
        public IModelBuilder<TModel> For<TModel>(string name) where TModel : class => inner.For<TModel>(name);
        public TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder => inner.Use<TModelBuilder>();
        public IModelBuilder Use(Type modelBuilderType) => inner.Use(modelBuilderType);
        public IModelBuilder<TModel> ForEmpty<TModel>() where TModel : class => inner.ForEmpty<TModel>();
        public TFaker Faker<TFaker>() where TFaker : IFaker => inner.Faker<TFaker>();
    }
}
