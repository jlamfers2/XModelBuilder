using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Core;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Covers the instancing fallbacks used by the new() token and by ForEmpty: parameterless ctor,
// fewest-parameters ctor with synthesized defaults, and the uninitialized-object last resort.
public class InstantiatorTests
{
    public sealed class HasParameterlessCtor
    {
        public string Marker { get; set; } = "ctor-ran";
    }

    public sealed class OnlyParameterizedCtor
    {
        public string Text { get; }
        public int Number { get; }
        public object? Reference { get; }

        public OnlyParameterizedCtor(string text, int number, object? reference)
        {
            Text = text;
            Number = number;
            Reference = reference;
        }
    }

    public sealed class ThrowingCtor
    {
        public string Set { get; set; } = "unset";

        public ThrowingCtor(int _) => throw new InvalidOperationException("ctor boom");
    }

    [Fact]
    public void CreateInstance_uses_the_parameterless_constructor_when_present()
    {
        // Act
        var instance = Instantiator.CreateInstance<HasParameterlessCtor>();

        // Assert
        Assert.Equal("ctor-ran", instance.Marker);
    }

    [Fact]
    public void CreateInstance_synthesizes_defaults_for_the_fewest_parameter_constructor()
    {
        // Act
        var instance = (OnlyParameterizedCtor)Instantiator.CreateInstance(typeof(OnlyParameterizedCtor));

        // Assert
        Assert.Equal(string.Empty, instance.Text); // string -> empty
        Assert.Equal(0, instance.Number);          // value type -> default
        Assert.Null(instance.Reference);           // reference type -> null
    }

    [Fact]
    public void CreateInstance_falls_back_to_uninitialized_object_when_the_constructor_throws()
    {
        // Act
        var instance = (ThrowingCtor)Instantiator.CreateInstance(typeof(ThrowingCtor));

        // Assert
        Assert.NotNull(instance);
        Assert.Null(instance.Set); // uninitialized: field initializer never ran
    }

    [Fact]
    public void CreateInstance_null_type_throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Instantiator.CreateInstance(null!));
    }

    [Fact]
    public void CreateInstance_type_without_an_accessible_constructor_throws()
    {
        // A value type has no reflectable instance constructor, so it hits the "no usable
        // constructor" branch (new() is meant for reference/model types, not primitives).
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => Instantiator.CreateInstance(typeof(int)));
    }

    // Integration: the new() token routes through Instantiator (a bare instance, no builder defaults).
    public sealed class TokenHost
    {
        public OnlyParameterizedCtor? Nested { get; set; }
    }

    [Fact]
    public void New_token_builds_a_bare_instance_via_the_instantiator()
    {
        // Arrange
        var xprovider = new ServiceCollection().AddXModelBuilder()
            .BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

        // Act
        var host = xprovider.For<TokenHost>().With("Nested", "new()").Build();

        // Assert
        Assert.NotNull(host.Nested);
        Assert.Equal(string.Empty, host.Nested!.Text);
    }
}
