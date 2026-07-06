using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class SeedFactoryTests
{
    // Stands in for "something scope-specific the seed is derived from" (e.g. a scenario title).
    private sealed class SeedSource
    {
        public int Seed { get; set; }
    }

    [Fact]
    public void SeedFactory_ConstantSeed_MatchesIntOverload()
    {
        // Arrange
        XFakerApi FromFactory() => new ServiceCollection().AddXModelBuilder().AddXFaker(_ => 42)
            .BuildServiceProvider().GetRequiredService<IModelBuilderProvider>().Faker<Faker>().XFake;
        XFakerApi FromInt() => new ServiceCollection().AddXModelBuilder().AddXFaker(42)
            .BuildServiceProvider().GetRequiredService<IModelBuilderProvider>().Faker<Faker>().XFake;

        // Act
        var byFactory = Enumerable.Range(0, 5).Select(_ => FromFactory().NewGuid()).ToList();
        var byInt = Enumerable.Range(0, 5).Select(_ => FromInt().NewGuid()).ToList();

        // Assert
        Assert.Equal(byInt, byFactory);
    }

    [Fact]
    public void SeedFactory_PerScope_DerivesSeedFromScopedState()
    {
        // Arrange
        var root = new ServiceCollection()
            .AddScoped<SeedSource>()
            .AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope)
            .AddXFaker(sp => sp.GetRequiredService<SeedSource>().Seed)
            .BuildServiceProvider();

        Guid FirstGuid(int seed)
        {
            using var scope = root.CreateScope();
            scope.ServiceProvider.GetRequiredService<SeedSource>().Seed = seed;
            return scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>().Faker<Faker>().XFake.NewGuid();
        }

        // Act & Assert
        Assert.Equal(FirstGuid(7), FirstGuid(7));      // same per-scope seed -> reproducible
        Assert.NotEqual(FirstGuid(7), FirstGuid(8));   // different per-scope seed -> different data
    }
}
