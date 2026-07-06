using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Core;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Top-up branch/line coverage for ValueConverter: every built-in primitive converter, the custom
// converter registration hook, the unsupported-input-shape guard and a couple of token branches.
public class ValueConverterBranchTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static IModelBuilderProvider Provider() =>
        new ServiceCollection().AddXModelBuilder().BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

    private static object? Convert(string input, Type type) => ValueConverter.Convert(input, type, Inv, Provider());

    [Fact]
    public void Every_builtin_primitive_converter_parses_its_type()
    {
        // Act & Assert
        Assert.Equal(true, Convert("true", typeof(bool)));
        Assert.Equal((byte)255, Convert("255", typeof(byte)));
        Assert.Equal((short)1000, Convert("1,000", typeof(short)));   // AllowThousands
        Assert.Equal(1_000_000L, Convert("1,000,000", typeof(long))); // AllowThousands
        Assert.Equal(3.14f, Convert("3.14", typeof(float)));
        Assert.Equal(3.14d, Convert("3.14", typeof(double)));
        Assert.Equal(3.14m, Convert("3.14", typeof(decimal)));
        Assert.Equal(new DateTime(2026, 7, 1), Convert("2026-07-01", typeof(DateTime)));
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 7, 1), TimeSpan.Zero), Convert("2026-07-01T00:00:00+00:00", typeof(DateTimeOffset)));
        Assert.Equal(new TimeSpan(1, 2, 3), Convert("01:02:03", typeof(TimeSpan)));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), Convert("11111111-1111-1111-1111-111111111111", typeof(Guid)));
        Assert.Equal('A', Convert("A", typeof(char)));
    }

    public readonly struct Temperature(int degrees)
    {
        public int Degrees { get; } = degrees;
    }

    [Fact]
    public void AddKnownTypeConverter_registers_a_custom_converter()
    {
        // Arrange
        ValueConverter.AddKnownTypeConverter(typeof(Temperature), (s, c) => new Temperature(int.Parse(s, c)));

        // Act
        var result = Convert("42", typeof(Temperature));

        // Assert
        Assert.Equal(42, Assert.IsType<Temperature>(result).Degrees);
    }

    [Fact]
    public void ConvertObject_with_unsupported_input_shape_throws_NotSupported()
    {
        // Act & Assert - an int is neither string, object[], nor a key/value sequence.
        Assert.Throws<NotSupportedException>(() => ValueConverter.ConvertObject(42, typeof(string), Inv, Inv, Provider()));
    }

    [Fact]
    public void Default_token_for_nullable_value_type_returns_null()
    {
        // Act & Assert
        Assert.Null(Convert("default()", typeof(int?)));
    }

    [Fact]
    public void Double_leading_at_escapes_and_keeps_a_single_at_in_the_literal()
    {
        // Act - one '@' is stripped as the escape; the remaining "@new()" is literal text.
        var result = Convert("@@new()", typeof(string));

        // Assert
        Assert.Equal("@new()", result);
    }

    [Fact]
    public void Unparseable_primitive_is_wrapped_in_a_FormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => Convert("not-a-number", typeof(int)));
    }

    public sealed class Poco
    {
        public string Known { get; set; } = "";
    }

    [Fact]
    public void Object_literal_naming_an_unknown_member_throws_InvalidOperation()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => Convert("{Ghost:1}", typeof(Poco)));
    }
}
