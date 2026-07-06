using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Covers assembly scanning: the public AddModelBuildersFromAssembly / AddModelBuildersFromAssemblies
// registration helpers and the internal AssemblyScanner primitives they build on (including the
// graceful-degradation and argument-validation branches).
public class AssemblyScannerTests
{
    public sealed class ScanModel
    {
        public string Name { get; set; } = "scan-default";
    }

    [ModelBuilder("scan")]
    public sealed class ScanModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<ScanModelBuilder, ScanModel>(options, xprovider)
    {
        protected override void SetDefaults() => With(x => x.Name, "scanned");
    }

    [Fact]
    public void AddModelBuildersFromAssembly_registers_builders_found_in_the_assembly()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(ScanModelBuilder).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

        // Act
        var built = provider.Use<ScanModelBuilder>().Build();

        // Assert
        Assert.Equal("scanned", built.Name);
    }

    [Fact]
    public void AddModelBuildersFromAssemblies_scans_the_whole_domain_and_registers_exported_builders()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssemblies()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

        // Act
        var built = provider.Use<ScanModelBuilder>().Build();

        // Assert
        Assert.Equal("scanned", built.Name);
    }

    [Fact]
    public void GetExportedTypes_is_cached_returning_the_same_instance()
    {
        // Arrange & Act
        var first = AssemblyScanner.GetExportedTypes();
        var second = AssemblyScanner.GetExportedTypes();

        // Assert
        Assert.NotEmpty(first);
        Assert.Same(first, second); // cached until a new assembly loads
    }

    [Fact]
    public void GetAllTypesFromAssembly_returns_types_and_rejects_null()
    {
        // Arrange
        var assembly = typeof(ScanModelBuilder).Assembly;

        // Act
        var types = assembly.GetAllTypesFromAssembly();

        // Assert
        Assert.Contains(typeof(ScanModelBuilder), types);
        Assert.Throws<ArgumentNullException>(() => ((Assembly)null!).GetAllTypesFromAssembly());
    }

    [Fact]
    public void GetAllTypesFromAppDomain_and_GetExportedTypesFromAppDomain_enumerate_and_reject_null()
    {
        // Arrange
        var domain = AppDomain.CurrentDomain;

        // Act
        var allTypes = domain.GetAllTypesFromAppDomain().ToList();
        var exported = domain.GetExportedTypesFromAppDomain().ToList();

        // Assert
        Assert.Contains(typeof(ScanModelBuilder), allTypes);
        Assert.Contains(typeof(ScanModelBuilder), exported);
        Assert.Throws<ArgumentNullException>(() => ((AppDomain)null!).GetAllTypesFromAppDomain().ToList());
        Assert.Throws<ArgumentNullException>(() => ((AppDomain)null!).GetExportedTypesFromAppDomain().ToList());
    }

    [Fact]
    public void GetAllAssemblies_returns_loaded_assemblies_and_rejects_null_domain()
    {
        // Arrange & Act
        var assemblies = AssemblyScanner.GetAllAssemblies().ToList();

        // Assert
        Assert.Contains(typeof(ScanModelBuilder).Assembly, assemblies);
        Assert.Throws<ArgumentNullException>(() => ((AppDomain)null!).GetAllAssemblies().ToList());
    }

    [Fact]
    public void GetAssembliesFromDirectory_loads_from_the_base_directory()
    {
        // Arrange
        var baseDir = AppContext.BaseDirectory;

        // Act
        var assemblies = AppDomain.CurrentDomain.GetAssembliesFromDirectory(baseDir);

        // Assert
        Assert.NotEmpty(assemblies);
    }

    [Fact]
    public void GetAssembliesFromDirectory_missing_directory_throws_and_null_args_are_rejected()
    {
        // Arrange
        var missing = Path.Combine(Path.GetTempPath(), "xmb-does-not-exist-" + Guid.NewGuid().ToString("N"));

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => AppDomain.CurrentDomain.GetAssembliesFromDirectory(missing));
        Assert.Throws<ArgumentNullException>(() => ((AppDomain)null!).GetAssembliesFromDirectory("x"));
        Assert.Throws<ArgumentNullException>(() => AppDomain.CurrentDomain.GetAssembliesFromDirectory(null!));
    }

    [Fact]
    public void EnsureAssemblyIsLoaded_returns_the_assembly_for_a_real_dll()
    {
        // Arrange
        var realDll = typeof(ScanModelBuilder).Assembly.Location;

        // Act
        var loaded = AppDomain.CurrentDomain.EnsureAssemblyIsLoaded(realDll);

        // Assert
        Assert.Equal(typeof(ScanModelBuilder).Assembly, loaded);
    }

    [Fact]
    public void EnsureAssemblyIsLoaded_returns_null_for_missing_file_and_for_a_non_pe_dll()
    {
        // Arrange
        var missing = Path.Combine(Path.GetTempPath(), "xmb-missing-" + Guid.NewGuid().ToString("N") + ".dll");
        var garbage = Path.Combine(Path.GetTempPath(), "xmb-garbage-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllText(garbage, "this is not a PE image");

        try
        {
            // Act
            var missingResult = AppDomain.CurrentDomain.EnsureAssemblyIsLoaded(missing); // FileNotFound -> null
            var garbageResult = AppDomain.CurrentDomain.EnsureAssemblyIsLoaded(garbage); // BadImageFormat -> null

            // Assert
            Assert.Null(missingResult);
            Assert.Null(garbageResult);
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    public void EnsureAssemblyIsLoaded_rejects_null_arguments()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((AppDomain)null!).EnsureAssemblyIsLoaded("x.dll"));
        Assert.Throws<ArgumentNullException>(() => AppDomain.CurrentDomain.EnsureAssemblyIsLoaded(null!));
    }
}
