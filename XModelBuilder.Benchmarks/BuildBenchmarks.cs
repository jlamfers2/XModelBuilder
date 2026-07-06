using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Benchmarks;

/// <summary>
/// Representative build scenarios that exercise the per-Build hotspots: scalar set (lambda vs
/// string), typed conversion, deep-path member resolution, nested object-literal parsing, faker
/// token dispatch, a table-style WithValues row, and BuildMany. MemoryDiagnoser reports allocations
/// per op, which are a big part of the cost story.
/// </summary>
[MemoryDiagnoser]
public class BuildBenchmarks
{
    public class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    public class Person
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public Address Address { get; set; } = new();
    }

    public class BenchFakers : IFaker
    {
        public string RandomString() => "x";
    }

    private static readonly KeyValuePair<string, string?>[] Row =
    [
        new("Name", "John"),
        new("Count", "42"),
        new("Address.Street", "Main"),
        new("Address.City", "Amsterdam"),
    ];

    private IModelBuilderProvider _xprovider = null!;

    [GlobalSetup]
    public void Setup()
    {
        _xprovider = new ServiceCollection()
            .AddXModelBuilder()
            .AddFaker<BenchFakers>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();
    }

    [Benchmark]
    public IModelBuilder<Person> ForResolutionOnly() => _xprovider.For<Person>();

    [Benchmark(Baseline = true)]
    public Person ScalarLambda() => _xprovider.For<Person>().With(x => x.Name, "John").Build();

    [Benchmark]
    public Person ScalarString() => _xprovider.For<Person>().With("Name", "John").Build();

    [Benchmark]
    public Person IntConversion() => _xprovider.For<Person>().With("Count", "42").Build();

    [Benchmark]
    public Person DeepPathString() => _xprovider.For<Person>().With("Address.Street", "Main").Build();

    [Benchmark]
    public Person ObjectLiteral() =>
        _xprovider.For<Person>().With("Address", "{Street:\"Main\",City:\"Amsterdam\"}").Build();

    [Benchmark]
    public Person FakerToken() => _xprovider.For<Person>().With("Name", "RandomString()").Build();

    [Benchmark]
    public Person WithValuesRow() => _xprovider.For<Person>().WithValues(Row).Build();

    [Benchmark]
    public IReadOnlyList<Person> BuildMany10() =>
        _xprovider.For<Person>().With(x => x.Name, "John").BuildMany(10);
}
