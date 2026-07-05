using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class XFakerTests
{
    public class Person
    {
        public string Name { get; set; } = "";
        public Guid Id { get; set; }
    }

    // The ServiceProvider is the determinism/isolation boundary: a fresh provider per call gets its
    // own seeded Random, so two providers with the same seed reproduce each other exactly, and a
    // fresh provider starts its counters over.
    private static IModelBuilderProvider CreateProvider(int seed = 8675309) =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddXFaker(seed)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void SameSeed_ProducesIdentical_NewGuidSequence()
    {
        var a = CreateProvider(123).Faker<Faker>();
        var b = CreateProvider(123).Faker<Faker>();

        var seqA = Enumerable.Range(0, 5).Select(_ => a.NewGuid()).ToList();
        var seqB = Enumerable.Range(0, 5).Select(_ => b.NewGuid()).ToList();

        Assert.Equal(seqA, seqB);
        Assert.DoesNotContain(Guid.Empty, seqA);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferent_NewGuidSequence()
    {
        var a = CreateProvider(1).Faker<Faker>();
        var b = CreateProvider(2).Faker<Faker>();

        var seqA = Enumerable.Range(0, 5).Select(_ => a.NewGuid()).ToList();
        var seqB = Enumerable.Range(0, 5).Select(_ => b.NewGuid()).ToList();

        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void NewGuid_ByName_IsStable_AcrossProviders_And_OrderIndependent()
    {
        var a = CreateProvider(1).Faker<Faker>();
        var b = CreateProvider(999).Faker<Faker>(); // different seed on purpose

        // Same name -> same GUID regardless of seed, provider or call order.
        Assert.Equal(a.NewGuid("customer:acme"), b.NewGuid("customer:acme"));
        Assert.Equal(a.NewGuid("customer:acme"), a.NewGuid("customer:acme"));
        // Different names -> different GUIDs.
        Assert.NotEqual(a.NewGuid("customer:acme"), a.NewGuid("customer:globex"));
    }

    [Fact]
    public void NextId_IsMonotonic_PerProvider_And_ResetsInFreshProvider()
    {
        var f = CreateProvider().Faker<Faker>();
        Assert.Equal(1L, f.NextId());
        Assert.Equal(2L, f.NextId());
        Assert.Equal(3L, f.NextId());

        var fresh = CreateProvider().Faker<Faker>();
        Assert.Equal(1L, fresh.NextId());
    }

    [Fact]
    public void NamedCounters_AreIndependent()
    {
        var f = CreateProvider().Faker<Faker>();

        Assert.Equal(1L, f.NextId("order"));
        Assert.Equal(1L, f.NextId("invoice"));
        Assert.Equal(2L, f.NextId("order"));
        Assert.Equal(2L, f.NextId("invoice"));
    }

    [Fact]
    public void Sequence_FormatsWithCounter()
    {
        var f = CreateProvider().Faker<Faker>();

        Assert.Equal("INV-0001", f.Sequence("INV-{0:0000}"));
        Assert.Equal("INV-0002", f.Sequence("INV-{0:0000}"));
    }

    [Fact]
    public void AgeBetween_IsDeterministic_And_WithinRange()
    {
        var atDate = new DateTime(2026, 1, 1);
        var a = CreateProvider(7).Faker<Faker>();
        var b = CreateProvider(7).Faker<Faker>();

        var birthA = a.AgeBetween(20, 30, atDate);
        var birthB = b.AgeBetween(20, 30, atDate);

        Assert.Equal(birthA, birthB);
        Assert.InRange(atDate.Year - birthA.Year, 20, 30);
    }

    [Fact]
    public void IntBetween_IsInclusive()
    {
        var f = CreateProvider().Faker<Faker>();

        Assert.Equal(5, f.IntBetween(5, 5));
        Assert.InRange(f.IntBetween(1, 3), 1, 3);
    }

    [Fact]
    public void Token_NewGuid_BuildsDeterministically_AcrossProviders()
    {
        var p1 = CreateProvider(99).For<Person>().With("Id", "NewGuid()").Build();
        var p2 = CreateProvider(99).For<Person>().With("Id", "NewGuid()").Build();

        Assert.Equal(p1.Id, p2.Id);
        Assert.NotEqual(Guid.Empty, p1.Id);
    }
}
