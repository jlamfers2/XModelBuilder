using System.Collections.Concurrent;
using System.Reflection;

namespace XModelBuilder.DependencyInjection;

/// <summary>
/// Discovers types and assemblies across the current <see cref="AppDomain"/> for builder/faker
/// auto-registration. Loads assemblies from the bin folder on demand, caches the exported-type set
/// (invalidated on assembly load), and degrades gracefully when some assemblies or their
/// dependencies fail to load.
/// </summary>
internal static class AssemblyScanner
{
    // Lazy<bool> so the per-domain bin scan runs exactly once, even under concurrency.
    private static readonly ConcurrentDictionary<AppDomain, Lazy<bool>> _completedDomains = new();

    // Cached exported types, invalidated whenever a new assembly is loaded into the domain.
    private static volatile IReadOnlyList<Type>? _exportedTypes;

#pragma warning disable S3963
    static AssemblyScanner()
#pragma warning restore S3963
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => _exportedTypes = null;
    }

    /// <summary>
    /// Returns all exported (publicly visible) types across the current app domain, cached until a
    /// new assembly is loaded.
    /// </summary>
    /// <returns>The cached list of exported types.</returns>
    public static IReadOnlyList<Type> GetExportedTypes()
        => _exportedTypes ??= [.. AppDomain.CurrentDomain.GetExportedTypesFromAppDomain()];

    /// <summary>
    /// Returns all types defined in the given assembly, falling back to the successfully loaded
    /// types when some types cannot be loaded.
    /// </summary>
    /// <param name="assembly">The assembly to enumerate.</param>
    /// <returns>The types that could be loaded from the assembly.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <see langword="null"/>.</exception>
    public static Type[] GetAllTypesFromAssembly(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            // GetTypes() already returns a fresh Type[]; no extra ToArray() needed.
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // We still have access to the types that loaded successfully.
            return e.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }
    }

    /// <summary>
    /// Enumerates all types from every non-dynamic assembly currently available in the app domain.
    /// </summary>
    /// <param name="appDomain">The app domain to scan.</param>
    /// <returns>All loadable types across the domain's assemblies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Type> GetAllTypesFromAppDomain(this AppDomain appDomain)
    {
        ArgumentNullException.ThrowIfNull(appDomain);

        return appDomain
            .GetAllAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => a.GetAllTypesFromAssembly());
    }

    /// <summary>
    /// Enumerates all exported (publicly visible) types from every non-dynamic assembly currently
    /// available in the app domain.
    /// </summary>
    /// <param name="appDomain">The app domain to scan.</param>
    /// <returns>All exported types across the domain's assemblies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Type> GetExportedTypesFromAppDomain(this AppDomain appDomain)
    {
        ArgumentNullException.ThrowIfNull(appDomain);

        return appDomain
            .GetAllAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => a.GetExportedTypesSafe());
    }

    // Mirrors Assembly.GetExportedTypes() but degrades gracefully when dependent
    // assemblies fail to load. GetExportedTypes() throws FileNotFoundException in that
    // case, whereas going through the safe loader and filtering on Type.IsVisible
    // (the precise "exported" semantics, walking the full nesting chain) yields the
    // types that are actually available.
    private static IEnumerable<Type> GetExportedTypesSafe(this Assembly assembly)
        => assembly.GetAllTypesFromAssembly().Where(t => t.IsVisible);

    /// <summary>
    /// Returns all assemblies available in the current app domain, ensuring the bin folder has been
    /// scanned first.
    /// </summary>
    /// <returns>The available assemblies.</returns>
    public static IEnumerable<Assembly> GetAllAssemblies()
        => AppDomain.CurrentDomain.GetAllAssemblies();

    /// <summary>
    /// Returns all assemblies available in the given app domain, ensuring the bin folder has been
    /// scanned first.
    /// </summary>
    /// <param name="appDomain">The app domain to enumerate.</param>
    /// <returns>The available assemblies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Assembly> GetAllAssemblies(this AppDomain appDomain)
    {
        ArgumentNullException.ThrowIfNull(appDomain);

        return appDomain.EnsureAvailableAssembliesLoaded().GetAssemblies();
    }

    /// <summary>
    /// Loads and returns the assemblies found in the given directory (relative paths are resolved
    /// against the app domain's bin folder), optionally recursing into subdirectories and
    /// de-duplicating by assembly identity.
    /// </summary>
    /// <param name="appDomain">The app domain to load the assemblies into.</param>
    /// <param name="path">The directory to scan; resolved against the bin folder when relative.</param>
    /// <param name="searchRecursively">Whether to also scan subdirectories.</param>
    /// <returns>The loaded assemblies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory cannot be found relative to the current or bin folder.</exception>
    public static IList<Assembly> GetAssembliesFromDirectory(this AppDomain appDomain, string path, bool searchRecursively = false)
    {
        ArgumentNullException.ThrowIfNull(appDomain);
        ArgumentNullException.ThrowIfNull(path);

        if (!Directory.Exists(path))
        {
            var binFolder = !string.IsNullOrEmpty(appDomain.RelativeSearchPath)
                ? Path.Combine(appDomain.BaseDirectory, appDomain.RelativeSearchPath)
                : appDomain.BaseDirectory;
            path = Path.Combine(binFolder, path);
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException("Path not found: " + path);
            }
        }

        var list = Directory.GetFiles(path, "*.dll")
            // .Union(Directory.GetFiles(path, "*.exe"))
            .Select(s => appDomain.EnsureAssemblyIsLoaded(s))
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        if (searchRecursively)
        {
            foreach (var subfolder in Directory.GetDirectories(path))
            {
                list.AddRange(appDomain.GetAssembliesFromDirectory(subfolder, true));
            }

            // Dedup by identity in case the same assembly is found in multiple folders.
            return list
                .GroupBy(a => a.FullName)
                .Select(g => g.First())
                .ToList();
        }

        return list;
    }

    /// <summary>
    /// Ensures the app domain's bin folder has been scanned and its assemblies loaded. The scan runs
    /// exactly once per domain, even under concurrency.
    /// </summary>
    /// <param name="appDomain">The app domain to prepare.</param>
    /// <returns>The same app domain, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> is <see langword="null"/>.</exception>
    public static AppDomain EnsureAvailableAssembliesLoaded(this AppDomain appDomain)
    {
        ArgumentNullException.ThrowIfNull(appDomain);

        // The Lazy guarantees the directory scan runs exactly once per domain,
        // even when several threads reach this point concurrently.
        _ = _completedDomains.GetOrAdd(appDomain, d => new Lazy<bool>(() =>
        {
            var binFolder = !string.IsNullOrEmpty(d.RelativeSearchPath)
                ? Path.Combine(d.BaseDirectory, d.RelativeSearchPath)
                : d.BaseDirectory;

            d.GetAssembliesFromDirectory(binFolder);
            return true;
        })).Value;

        return appDomain;
    }

    /// <summary>
    /// Ensures the assembly identified by the given file is loaded into the app domain, returning the
    /// already-loaded instance when present. Unmanaged, unresolvable or missing assemblies are skipped.
    /// </summary>
    /// <param name="appDomain">The app domain to load into.</param>
    /// <param name="assemblyFileName">The path to the assembly file.</param>
    /// <returns>The loaded assembly, or <see langword="null"/> when it could not be loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="appDomain"/> or <paramref name="assemblyFileName"/> is <see langword="null"/>.</exception>
    public static Assembly? EnsureAssemblyIsLoaded(this AppDomain appDomain, string assemblyFileName)
    {
        ArgumentNullException.ThrowIfNull(appDomain);
        ArgumentNullException.ThrowIfNull(assemblyFileName);

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyFileName);
            return appDomain.GetAssemblies()
                       .FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(assemblyName, a.GetName()))
                   ?? appDomain.Load(assemblyName);
        }
        catch (BadImageFormatException)
        {
            // Thrown by GetAssemblyName for an unmanaged/native DLL. Skip it.
        }
        catch (FileLoadException)
        {
            // Managed assembly whose dependencies could not be resolved. Skip it.
        }
        catch (FileNotFoundException)
        {
            // Assembly or a dependency went missing between enumeration and load. Skip it.
        }

        return null;
    }
}