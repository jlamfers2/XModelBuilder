using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.Bogus;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class BogusFakerTests
{
    public class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public Guid Id { get; set; }
    }

    private static IModelBuilderProvider CreateProvider(int seed = 8675309) =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddBogusFaker(seed)
            .AddXFaker(seed)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void SameSeed_ProducesIdentical_FirstNameSequence()
    {
        var a = CreateProvider(42).Faker<BogusFaker>();
        var b = CreateProvider(42).Faker<BogusFaker>();

        var seqA = Enumerable.Range(0, 5).Select(_ => a.Bogus.Name.FirstName()).ToList();
        var seqB = Enumerable.Range(0, 5).Select(_ => b.Bogus.Name.FirstName()).ToList();

        Assert.Equal(seqA, seqB);
        Assert.DoesNotContain(seqA, string.IsNullOrEmpty);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferent_FirstNameSequence()
    {
        var a = CreateProvider(1).Faker<BogusFaker>();
        var b = CreateProvider(2).Faker<BogusFaker>();

        var seqA = Enumerable.Range(0, 5).Select(_ => a.Bogus.Name.FirstName()).ToList();
        var seqB = Enumerable.Range(0, 5).Select(_ => b.Bogus.Name.FirstName()).ToList();

        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void TypedRoute_ExposesUnderlyingBogusFaker_ForTheLongTail()
    {
        var faker = CreateProvider(42).Faker<BogusFaker>();

        var county = faker.Bogus.Address.County();

        Assert.False(string.IsNullOrEmpty(county));
    }

    [Fact]
    public void DeepPathToken_ResolvesBogusGenerator_Deterministically_AcrossProviders()
    {
        var p1 = CreateProvider(99).For<Person>()
            .With("Name", "bogus.name.firstname()")
            .With("City", "bogus.address.city()")
            .With("Id", "newGuid()")
            .BuildMany(10);
        var p2 = CreateProvider(99).For<Person>()
            .With("Name", "bogus.name.firstname()")
            .With("City", "bogus.address.city()")
            .With("Id", "newGuid()")
            .BuildMany(10);

        Assert.Equal(p1, p2, EqualityComparer<Person>.Create(
            (a, b) => a!.Name == b!.Name && a.City == b.City && a.Id == b.Id));
    }

    [Fact]
    public void DeepPathReferences_ResolvesBogusGenerator_Deterministically_AcrossProviders()
    {
        var p1 = CreateProvider(99).For<Person>()
            .With(x => x.Name, p => p.Bogus().Name.FirstName())
            .With(x => x.City, p => p.Bogus().Address.City())
            .With(x => x.Id, p => p.Faker<Faker>().NewGuid())
            .BuildMany(10);

        var p2 = CreateProvider(99).For<Person>()
            .With(x => x.Name, p => p.Bogus().Name.FirstName())
            .With(x => x.City, p => p.Bogus().Address.City())
            .With(x => x.Id, p => p.Faker<Faker>().NewGuid())
            .BuildMany(10);

        Assert.Equal(p1, p2, EqualityComparer<Person>.Create(
            (a, b) => a!.Name == b!.Name && a.City == b.City && a.Id == b.Id));
    }

    [Fact]
    public void Locale_IsConfigurable()
    {
        var faker = new ServiceCollection()
            .AddXModelBuilder()
            .AddBogusFaker(seed: 42, locale: "nl")
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>()
            .Faker<BogusFaker>();

        Assert.Equal("nl", faker.Bogus.Locale);
    }

    [Fact]
    public void DeepPathToken_TerminalProperty_Fallback_ReachesBogusPersonProperty()
    {
        // Bogus' pre-built person exposes FirstName as a PROPERTY (not a method); the terminal
        // method-then-property fallback must still reach it.
        var person = CreateProvider(7).For<Person>()
            .With("Name", "bogus.person.firstname()")
            .Build();

        Assert.False(string.IsNullOrEmpty(person.Name));
    }
}
