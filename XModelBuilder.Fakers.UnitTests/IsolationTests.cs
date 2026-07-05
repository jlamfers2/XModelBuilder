using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class IsolationTests
{
    private static long FirstIdInNewScope(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>().Faker<Faker>().NextId();
    }

    private static List<Guid> ThreeGuidsInNewScope(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        var faker = scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>().Faker<Faker>();
        return [faker.NewGuid(), faker.NewGuid(), faker.NewGuid()];
    }

    [Fact]
    public void Shared_State_IsSharedAcrossScopes()
    {
        var root = new ServiceCollection()
            .AddXModelBuilder(isolation: XModelBuilderIsolation.Shared)
            .AddXFaker(seed: 123)
            .BuildServiceProvider();

        // One shared singleton faker -> the counter keeps climbing across scopes.
        Assert.Equal(1L, FirstIdInNewScope(root));
        Assert.Equal(2L, FirstIdInNewScope(root));
    }

    [Fact]
    public void PerScope_State_IsIsolated_And_EachScopeReproducible()
    {
        var root = new ServiceCollection()
            .AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope)
            .AddXFaker(seed: 123)
            .BuildServiceProvider();

        // Each scope gets its own re-seeded faker -> the counter resets every scope (isolation)...
        Assert.Equal(1L, FirstIdInNewScope(root));
        Assert.Equal(1L, FirstIdInNewScope(root));

        // ...and the seeded sequence is identical per scope (reproducible).
        Assert.Equal(ThreeGuidsInNewScope(root), ThreeGuidsInNewScope(root));
    }

    [Fact]
    public void Isolation_IsApplied_RegardlessOfCallOrder()
    {
        // AddXFaker BEFORE AddXModelBuilder: the deferred registration is flushed with the
        // PerScope lifetime once AddXModelBuilder sets the isolation.
        var root = new ServiceCollection()
            .AddXFaker(seed: 123)
            .AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope)
            .BuildServiceProvider();

        Assert.Equal(1L, FirstIdInNewScope(root));
        Assert.Equal(1L, FirstIdInNewScope(root));
    }

    [Fact]
    public void Validate_Throws_OnConflictingIsolation()
    {
        var services = new ServiceCollection()
            .AddXModelBuilder(isolation: XModelBuilderIsolation.Shared)
            .AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope); // conflict: provider stays Singleton

        var ex = Assert.Throws<InvalidOperationException>(() => services.ValidateXModelBuilderRegistrations());
        Assert.Contains("does not match the configured isolation", ex.Message);
    }
}
