using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Exhaustive tests for ModelBuilder<TBuilder, TModel>: they hit every public method, every
// constructor branch (parameterless / no public ctor / single ctor / multiple ctors), the full
// CtorParameterInfo.GetValue decision tree, deep-path application (lambda / string / indexer),
// Extend, and the explicit IModelBuilder<TModel> and IModelBuilder implementations.
public class ModelBuilderCoverageTests
{
    // ---- Models ---------------------------------------------------------------------------------

    public class Nested
    {
        public string? Value { get; set; }
    }

    // Parameterless -> _useStandardActivator == true.
    public class StandardModel
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Description { get; set; }
        public string? City { get; set; }
        public Nested Nested { get; set; } = new();
        public List<string> Items { get; set; } = [];
    }

    // Only a private ctor -> GetConstructors() empty -> _modelCtor == null -> Instantiator fallback.
    public class PrivateCtorModel
    {
        public string? Name { get; set; }
        public string? Extra { get; set; }
        private PrivateCtorModel() { }
    }

    // One ctor with required + optional parameters -> covers the whole GetValue decision tree.
    public class RichCtorModel
    {
        public RichCtorModel(string name, int count, string? optional = "def")
        {
            Name = name;
            Count = count;
            Optional = optional;
        }

        public string Name { get; }
        public int Count { get; }
        public string? Optional { get; }
        public string? Extra { get; set; }
        public Nested? Nested { get; set; }
    }

    // Multiple ctors -> FindModelCtor picks the one with the fewest parameters.
    public class MultiCtorModel
    {
        public string A { get; }
        public string B { get; }

        public MultiCtorModel(string a)
        {
            A = a;
            B = "b1";
        }

        public MultiCtorModel(string a, string b)
        {
            A = a;
            B = b;
        }
    }

    // Ctor parameter without a matching member -> covers the "skip" in ApplyCtorArgumentsAsMembers.
    public class PhantomCtorModel
    {
        public PhantomCtorModel(int hidden) => Visible = hidden * 2;

        public int Visible { get; set; }
    }

    // The only ctor has exclusively optional parameters -> covers the All(p => p.IsOptional) branch
    // in FindModelCtor. Deliberately NOT built (Activator cannot invoke such a ctor).
    public class AllOptionalCtorModel
    {
        public AllOptionalCtorModel(int x = 5) => X = x;

        public int X { get; set; }
    }

    // ---- Builders -------------------------------------------------------------------------------

    // Custom builder for StandardModel that exposes the protected members so the defensive branches
    // can be tested directly, and that counts how often SetDefaults runs.
    [ModelBuilder("probe")]
    public sealed class ProbeBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<ProbeBuilder, StandardModel>(options, xmodels)
    {
        public int SetDefaultsCallCount { get; private set; }

        protected override void SetDefaults() => SetDefaultsCallCount++;

        public Exception? CaptureEmptySettingError()
            => Record.Exception(() => ApplyDeepPathSetting(new StandardModel(), new DeepPathSetting()));

        public void ApplyDeepPathValueOnNullModel() => ApplyDeepPathValue(null!, "Name", "x");

        public void ApplyDeepPathExpressionOnNullModel()
        {
            Expression<Func<StandardModel, object?>> expr = m => m.Name;
            ApplyDeepPathExpression(null!, new DeepPathExpression { DeepPath = expr, Value = "x" });
        }

        public Exception? CaptureSetMemberNullModel()
            => Record.Exception(() => SetMember(null!, m => m.Name, "x"));

        public Exception? CaptureSetMemberNullMember()
            => Record.Exception(() => SetMember(new StandardModel(), (Expression<Func<StandardModel, string?>>)null!, "x"));
    }

    [ModelBuilder("rich")]
    public sealed class RichBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<RichBuilder, RichCtorModel>(options, xmodels)
    {
        protected override void SetDefaults() { }
    }

    [ModelBuilder("nested-builder")]
    public sealed class NestedBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<NestedBuilder, Nested>(options, xmodels)
    {
        protected override void SetDefaults() => With(n => n.Value, "nested-default");
    }

    [ModelBuilder("all-optional")]
    public sealed class AllOptionalBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<AllOptionalBuilder, AllOptionalCtorModel>(options, xmodels)
    {
        protected override void SetDefaults() { }
    }

    private static IModelBuilderProvider Provider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<ProbeBuilder>()
            .AddModelBuilder<RichBuilder>()
            .AddModelBuilder<NestedBuilder>()
            .AddModelBuilder<AllOptionalBuilder>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    // ---- Constructor ----------------------------------------------------------------------------

    [Fact]
    public void Ctor_NullOptions_Throws()
    {
        // Arrange
        var provider = Provider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ProbeBuilder(null!, provider));
    }

    [Fact]
    public void Ctor_NullProvider_Throws()
    {
        // Arrange
        var options = Options.Create(new ModelBuilderOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ProbeBuilder(options, null!));
    }

    // ---- ModelType ------------------------------------------------------------------------------

    [Fact]
    public void ModelType_ReturnsTheModelType()
    {
        // Arrange
        var provider = Provider();

        // Act
        var genericType = provider.For<StandardModel>().ModelType;
        var nonGenericType = provider.For(typeof(StandardModel)).ModelType;

        // Assert
        Assert.Equal(typeof(StandardModel), genericType);
        Assert.Equal(typeof(StandardModel), nonGenericType);
    }

    // ---- Reset ----------------------------------------------------------------------------------

    [Fact]
    public void Reset_ReInvokesSetDefaults_AndClearsConfiguredValues()
    {
        // Arrange
        var pb = Provider().Use<ProbeBuilder>();
        Assert.Equal(1, pb.SetDefaultsCallCount); // already happened from the constructor
        pb.With(x => x.Name, "temp");

        // Act
        var same = pb.Reset();
        var model = pb.Build();

        // Assert
        Assert.Same(pb, same);
        Assert.Equal(2, pb.SetDefaultsCallCount);
        Assert.Null(model.Name); // the earlier value has been cleared
    }

    // ---- CreateInstance branches ----------------------------------------------------------------

    [Fact]
    public void Build_ParameterlessModel_UsesStandardActivator()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().With(x => x.Name, "S");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("S", model.Name);
    }

    [Fact]
    public void Build_ModelWithoutPublicCtor_FallsBackToInstantiator()
    {
        // Arrange
        var builder = Provider().For<PrivateCtorModel>()
            .With("Name", "Z")          // _modelCtor == null -> HandleCtorArgument(string) returns false
            .With(x => x.Extra, "E");   // _modelCtor == null -> HandleCtorArgument<TValue> returns false

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("Z", model.Name);
        Assert.Equal("E", model.Extra);
    }

    [Fact]
    public void Build_MultipleCtors_PicksTheOneWithFewestParameters()
    {
        // Arrange
        var builder = Provider().For<MultiCtorModel>().With(x => x.A, "hi");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("hi", model.A);
        Assert.Equal("b1", model.B); // the 1-parameter ctor was chosen
    }

    [Fact]
    public void FindModelCtor_AllOptionalCtor_MarksStandardActivator()
    {
        // Arrange
        var provider = Provider();

        // Act
        // Merely instantiating the builder triggers the static FindModelCtor, where the ctor with
        // exclusively optional parameters hits the All(p => p.IsOptional) branch. Build() is
        // deliberately not called, because Activator cannot construct an all-optional ctor.
        var builder = provider.Use<AllOptionalBuilder>();

        // Assert
        Assert.Equal(typeof(AllOptionalCtorModel), builder.ModelType);
    }

    // ---- CtorParameterInfo.GetValue decision tree -----------------------------------------------

    [Fact]
    public void CtorArg_StringValue_IsConverted_And_IntValue_IsBoxedThrough()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Name, "John")  // Value is string -> ValueConverter
            .With(x => x.Count, 5);     // Value != null, not a string -> used directly

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("John", model.Name);
        Assert.Equal(5, model.Count);
        Assert.Equal("def", model.Optional); // omitted -> parameter default (HasDefaultValue)
    }

    [Fact]
    public void CtorArg_ValueFactory_IsInvokedForEachBuild()
    {
        // Arrange
        var next = 8;
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Name, "F")
            .With(x => x.Count, () => ++next); // ValueFactory branch

        // Act
        var first = builder.Build();
        var second = builder.Build();

        // Assert
        Assert.Equal(9, first.Count);
        Assert.Equal(10, second.Count); // re-evaluated
    }

    [Fact]
    public void CtorArg_NullOnOptionalParameter_UsesParameterDefault()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Count, 1)
            .With(x => x.Optional, (string?)null); // Value null + parameter optional -> DefaultValue

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("def", model.Optional);
        Assert.Equal(1, model.Count);
        Assert.Null(model.Name); // required param omitted -> GetParameterDefaultValueOrNull == null
    }

    [Fact]
    public void CtorArg_NullOnRequiredParameter_PassesNull()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Count, 2)
            .With(x => x.Name, (string?)null); // Value null + not optional -> return null

        // Act
        var model = builder.Build();

        // Assert
        Assert.Null(model.Name);
        Assert.Equal(2, model.Count);
    }

    // ---- HandleCtorArgument (string) ------------------------------------------------------------

    [Fact]
    public void CtorArg_StringPath_TopSegmentMatchesCtorParameter()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With("Name", "Kees")  // idx == -1, matches ctor param
            .With("Count", "7");   // string -> int via ValueConverter

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("Kees", model.Name);
        Assert.Equal(7, model.Count);
    }

    [Fact]
    public void StringPath_NonCtorSimpleMember_GoesToDeepPathValue()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With("Count", "3")
            .With("Extra", "hello"); // no ctor param "extra" -> deep-path member

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("hello", model.Extra);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void StringPath_DottedNonCtorTopSegment_GoesToStringPathSetter()
    {
        // Arrange
        var builder = Provider().For<RichCtorModel>()
            .With("Count", "4")
            .With("Nested.Value", "deep"); // idx != -1, "Nested" is not a ctor param

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("deep", model.Nested!.Value);
        Assert.Equal(4, model.Count);
    }

    // ---- HandleCtorArgument<TValue> (lambda) ----------------------------------------------------

    [Fact]
    public void LambdaPath_DeepExpression_OnCtorModel_GoesToDeepPath()
    {
        // Arrange
        // GetShallowPropertyName returns null for a nested expression -> no ctor routing.
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Count, 5)
            .With(x => x.Nested!.Value, "lv");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("lv", model.Nested!.Value);
    }

    [Fact]
    public void LambdaPath_NonCtorMember_OnCtorModel_GoesToDeepPath()
    {
        // Arrange
        // The shallow name "Extra" exists as a member but not as a ctor parameter.
        var builder = Provider().For<RichCtorModel>()
            .With(x => x.Count, 6)
            .With(x => x.Extra, "ex");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("ex", model.Extra);
    }

    // ---- Deep-path application (string) ---------------------------------------------------------

    [Fact]
    public void With_DottedStringPath_UsesStringPathSetter()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().With("Nested.Value", "dv");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("dv", model.Nested.Value);
    }

    [Fact]
    public void With_IndexedStringPath_UsesStringPathSetter()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().With("Items[1]", "x");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("x", model.Items[1]);
    }

    [Fact]
    public void With_UnknownSimpleMember_ThrowsOnBuild()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().With("DoesNotExist", "x");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    // ---- Deep-path application (lambda: Value vs ValueFactory) ----------------------------------

    [Fact]
    public void With_LambdaValue_And_LambdaFactory_AreBothApplied()
    {
        // Arrange
        var counter = 0;
        var builder = Provider().For<StandardModel>()
            .With(x => x.Name, "V")              // DeepPathExpression.Value branch
            .With(x => x.Age, () => ++counter);  // DeepPathExpression.ValueFactory branch

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("V", model.Name);
        Assert.Equal(1, model.Age);
    }

    // ---- ApplyDeepPathSetting branches ----------------------------------------------------------

    [Fact]
    public void With_SingleStringValue_AppliedViaDeepPathValue()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().With("Name", "SV");

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("SV", model.Name);
    }

    [Fact]
    public void WithValues_MultipleValues_AppliedViaDeepPathValues()
    {
        // Arrange
        var builder = Provider().For<StandardModel>().WithValues(
        [
            new("Name", "WN"),
            new("City", "WC"),
        ]);

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("WN", model.Name);
        Assert.Equal("WC", model.City);
    }

    [Fact]
    public void ApplyDeepPathSetting_EmptySetting_ThrowsArgumentException()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>();

        // Act
        var ex = builder.CaptureEmptySettingError();

        // Assert
        Assert.IsType<ArgumentException>(ex);
    }

    [Fact]
    public void ApplyDeepPathValue_NullModel_IsNoOp()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>();

        // Act & Assert (must not throw)
        builder.ApplyDeepPathValueOnNullModel();
    }

    [Fact]
    public void ApplyDeepPathExpression_NullModel_IsNoOp()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>();

        // Act & Assert (must not throw)
        builder.ApplyDeepPathExpressionOnNullModel();
    }

    // ---- SetMember null-checks ------------------------------------------------------------------

    [Fact]
    public void SetMember_NullModel_Throws()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>();

        // Act
        var ex = builder.CaptureSetMemberNullModel();

        // Assert
        Assert.IsType<ArgumentNullException>(ex);
    }

    [Fact]
    public void SetMember_NullMember_Throws()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>();

        // Act
        var ex = builder.CaptureSetMemberNullMember();

        // Assert
        Assert.IsType<ArgumentNullException>(ex);
    }

    // ---- Extend ---------------------------------------------------------------------------------

    [Fact]
    public void Extend_NullInstance_Throws()
    {
        // Arrange
        var builder = Provider().For<StandardModel>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Extend(null!));
    }

    [Fact]
    public void Extend_AppliesCtorConfiguredValuesOntoExistingInstance()
    {
        // Arrange
        var provider = Provider();
        var basis = provider.For<RichCtorModel>().With(x => x.Name, "A").With(x => x.Count, 1).Build();

        // Act
        var extended = provider.For<RichCtorModel>()
            .With(x => x.Name, "B")   // ctor arg with matching member -> set via backing field
            .With(x => x.Count, 2)
            .Extend(basis);

        // Assert
        Assert.Same(basis, extended);
        Assert.Equal("B", extended.Name);
        Assert.Equal(2, extended.Count);
    }

    [Fact]
    public void Extend_CtorParameterWithoutMember_IsSkipped()
    {
        // Arrange
        var provider = Provider();
        var basis = provider.For<PhantomCtorModel>().With("hidden", "3").Build();
        Assert.Equal(6, basis.Visible);

        // Act
        var extended = provider.For<PhantomCtorModel>()
            .With("hidden", "10")        // "hidden" has no member -> skipped during Extend
            .With(x => x.Visible, 99)    // ordinary member -> applied
            .Extend(basis);

        // Assert
        Assert.Same(basis, extended);
        Assert.Equal(99, extended.Visible);
    }

    [Fact]
    public void Extend_DoesNotLeakState_SubsequentBuildMakesFreshInstance()
    {
        // Arrange
        var provider = Provider();
        var builder = provider.For<StandardModel>().With(x => x.Name, "keep");
        var existing = provider.For<StandardModel>().Build();

        // Act
        var extended = builder.Extend(existing);
        var fresh = builder.Build();

        // Assert
        Assert.Same(existing, extended);
        Assert.NotSame(existing, fresh);
        Assert.Equal("keep", fresh.Name);
    }

    // ---- Public (concrete) fluent methods via Use<TBuilder>() -----------------------------------

    [Fact]
    public void PublicFluentApi_OnConcreteBuilder_AppliesEverything()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>()
            .With(x => x.Name, "PN")                        // With(getter, value)
            .With(x => x.Age, () => 3)                      // With(getter, Func<TValue?>)
            .With(x => x.Description, _ => "PD")            // With(getter, Func<provider, TValue?>)
            .With("City", "PCity")                          // With(string, string)
            .WithBuilder(x => x.Nested, "nested-builder")   // WithBuilder(getter, name)
            .WithValues([new("Items", "[p]")]);             // WithValues

        // Act
        var built = builder.Build();                         // Build (virtual)

        // Assert
        Assert.Equal("PN", built.Name);
        Assert.Equal(3, built.Age);
        Assert.Equal("PD", built.Description);
        Assert.Equal("PCity", built.City);
        Assert.Equal("nested-default", built.Nested.Value);
        Assert.Equal(["p"], built.Items);
    }

    [Fact]
    public void PublicNestedBuilderOverload_OnConcreteBuilder_ConfiguresNestedModel()
    {
        // Arrange
        var builder = Provider().Use<ProbeBuilder>()
            .With(x => x.Nested, b => b.With(n => n.Value, "XX")); // With(getter, nested builder)

        // Act
        var model = builder.Build();

        // Assert
        Assert.Equal("XX", model.Nested.Value);
    }

    [Fact]
    public void PublicExtend_OnConcreteBuilder_ReturnsSameInstance()
    {
        // Arrange
        var provider = Provider();
        var basis = provider.Use<ProbeBuilder>().With(x => x.Name, "pbase").Build();

        // Act
        var extended = provider.Use<ProbeBuilder>().With(x => x.City, "pc").Extend(basis);

        // Assert
        Assert.Same(basis, extended);
        Assert.Equal("pc", extended.City);
        Assert.Equal("pbase", extended.Name);
    }

    // ---- Explicit IModelBuilder<TModel> implementations via For<T>() ----------------------------

    [Fact]
    public void GenericInterface_ExplicitImplementations_AreAllReachable()
    {
        // Arrange
        var provider = Provider();
        IModelBuilder<StandardModel> builder = provider.For<StandardModel>();
        builder.Reset(); // explicit Reset

        // Act
        var model = builder
            .With(x => x.Name, "GN")                        // With(getter, value)
            .With(x => x.Age, () => 7)                      // With(getter, Func<TValue?>)
            .With(x => x.Description, p => p is null ? "x" : "GD") // With(getter, Func<provider, TValue?>)
            .With("City", "GCity")                          // With(string, string)
            .With(x => x.Nested, b => b.With(n => n.Value, "gv")) // nested builder overload
            .WithValues([new("Items", "[x,y]")])            // WithValues
            .Build();

        // WithBuilder + Extend explicitly via the generic interface.
        var basis = provider.For<StandardModel>()
            .WithBuilder(x => x.Nested, "nested-builder")
            .Build();
        var extended = provider.For<StandardModel>().With(x => x.City, "EC").Extend(basis);

        // Assert
        Assert.Equal("GN", model.Name);
        Assert.Equal(7, model.Age);
        Assert.Equal("GD", model.Description);
        Assert.Equal("GCity", model.City);
        Assert.Equal("gv", model.Nested.Value);
        Assert.Equal(["x", "y"], model.Items);
        Assert.Equal("nested-default", basis.Nested.Value);
        Assert.Same(basis, extended);
        Assert.Equal("EC", extended.City);
    }

    // ---- Explicit non-generic IModelBuilder implementations via For(Type) ------------------------

    [Fact]
    public void NonGenericInterface_ExplicitImplementations_AreAllReachable()
    {
        // Arrange
        var provider = Provider();
        Expression<Func<StandardModel, object?>> nameExpr = m => m.Name;
        Expression<Func<StandardModel, object?>> ageExpr = m => (object?)m.Age;
        Expression<Func<StandardModel, object?>> descExpr = m => m.Description;
        Expression<Func<StandardModel, Nested>> nestedExpr = m => m.Nested;
        IModelBuilder builder = provider.For(typeof(StandardModel));
        builder.Reset(); // explicit non-generic Reset

        // Act
        builder.With(nameExpr, (object?)"N");                                  // With(lambda, object?)
        builder.With(ageExpr, (Func<object?>)(() => 42));                      // With(lambda, Func<object?>)
        builder.With(descExpr, (Func<IModelBuilderProvider, object?>)(p => "D")); // With(lambda, Func<provider, object?>)
        builder.With("City", "Zwolle");                                        // With(string, string)
        builder.WithBuilder(nestedExpr, "nested-builder");                     // WithBuilder(lambda, name)
        builder.WithValues([new("Items", "[a,b]")]);                           // WithValues
        var built = builder.Build();                                           // non-generic Build

        // Assert
        var model = Assert.IsType<StandardModel>(built);
        Assert.Equal("N", model.Name);
        Assert.Equal(42, model.Age);
        Assert.Equal("D", model.Description);
        Assert.Equal("Zwolle", model.City);
        Assert.Equal("nested-default", model.Nested.Value);
        Assert.Equal(["a", "b"], model.Items);
    }

    [Fact]
    public void NonGenericInterface_Extend_ReturnsSameInstance()
    {
        // Arrange
        var provider = Provider();
        var basis = (StandardModel)provider.For(typeof(StandardModel)).With("Name", "base").Build();
        IModelBuilder builder = provider.For(typeof(StandardModel));
        builder.With("City", "ExtCity");

        // Act
        var extended = builder.Extend(basis);

        // Assert
        Assert.Same(basis, extended);
        Assert.Equal("ExtCity", ((StandardModel)extended).City);
        Assert.Equal("base", ((StandardModel)extended).Name);
    }
}
