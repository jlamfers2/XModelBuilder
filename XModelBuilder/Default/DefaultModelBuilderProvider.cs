using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using XModelBuilder.Core;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Default;

/// <summary>
/// Process-wide singleton <see cref="IModelBuilderProvider"/> for use without an explicit
/// DI-container. Internally backed by a lazily-(re)built <see cref="IServiceProvider"/>, wrapped
/// in the same <see cref="ModelBuilderProvider"/> used by the DI integration - so this is just a
/// convenience shell around the DI-registration extensions, not a separate, hand-rolled
/// resolution implementation. Any Add*/Set* call invalidates the built provider; it is rebuilt
/// lazily on the next resolution, so registering (even interleaved with usage) keeps working.
/// </summary>
public sealed class DefaultModelBuilderProvider : IModelBuilderProvider, IFakerInvocationSource
{
    /// <summary>
    /// The process-wide singleton instance used by the standalone (non-DI) API.
    /// </summary>
    public static DefaultModelBuilderProvider Current { get; } = new();

    private readonly object _lock = new();
    private readonly ServiceCollection _services = [];
    private ModelBuilderProvider? _builtProvider;

    private DefaultModelBuilderProvider()
    {
        _services.AddXModelBuilder();
    }

    /// <inheritdoc/>
    public IModelBuilder<TModel> For<TModel>() where TModel : class => (IModelBuilder<TModel>)For(typeof(TModel));

    /// <inheritdoc/>
    public IModelBuilder For(Type modelType) => GetProvider().For(modelType);

    /// <inheritdoc/>
    public IModelBuilder For(Type modelType, string name) => GetProvider().For(modelType, name);

    /// <inheritdoc/>
    public IModelBuilder<TModel> For<TModel>(string name) where TModel : class => (IModelBuilder<TModel>)For(typeof(TModel), name);

    /// <inheritdoc/>
    public TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder => GetProvider().Use<TModelBuilder>();

    /// <inheritdoc/>
    public IModelBuilder Use(Type modelBuilderType) => GetProvider().Use(modelBuilderType);

    /// <inheritdoc/>
    public IModelBuilder<TModel> NewDefaultModelBuilder<TModel>() where TModel : class => GetProvider().NewDefaultModelBuilder<TModel>();

    /// <inheritdoc/>
    public TFaker Faker<TFaker>() where TFaker : IFaker => GetProvider().Faker<TFaker>();

    object? IFakerInvocationSource.InvokeFaker(string name, object?[] args, Type targetType, CultureInfo culture) =>
        ((IFakerInvocationSource)GetProvider()).InvokeFaker(name, args, targetType, culture);

    /// <summary>
    /// Registers <typeparamref name="TModelBuilder"/> as the open-generic default (fallback) builder,
    /// used for model types that have no dedicated builder.
    /// </summary>
    /// <typeparam name="TModelBuilder">The builder type to register as the default.</typeparam>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider SetDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder
    {
        return SetDefaultModelBuilder(typeof(TModelBuilder));
    }

    /// <summary>
    /// Registers the given builder type as the open-generic default (fallback) builder, used for
    /// model types that have no dedicated builder.
    /// </summary>
    /// <param name="defaultModelBuilderType">The builder type to register as the default.</param>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider SetDefaultModelBuilder(Type defaultModelBuilderType)
    {
        Mutate(s => s.AddDefaultModelBuilder(defaultModelBuilderType));
        return this;
    }

    /// <summary>
    /// Registers a dedicated builder <typeparamref name="TModelBuilder"/> for its model type.
    /// </summary>
    /// <typeparam name="TModelBuilder">The builder type to register.</typeparam>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider AddModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder
    {
        return AddModelBuilder(typeof(TModelBuilder));
    }

    /// <summary>
    /// Registers a dedicated builder for its model type.
    /// </summary>
    /// <param name="modelBuilderType">The builder type to register.</param>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider AddModelBuilder(Type modelBuilderType)
    {
        Mutate(s => s.AddModelBuilder(modelBuilderType));
        return this;
    }

    /// <summary>Marks a builder as the default for its model type (order-independent), the standalone
    /// equivalent of <see cref="ServiceCollectionExtensions.UseAsDefaultModelBuilder{TModelBuilder}"/>.</summary>
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder
    {
        return UseAsDefaultModelBuilder(typeof(TModelBuilder));
    }

    /// <summary>
    /// Marks the given builder as the default for its model type (order-independent), the standalone
    /// equivalent of <see cref="ServiceCollectionExtensions.UseAsDefaultModelBuilder(IServiceCollection, Type)"/>.
    /// </summary>
    /// <param name="modelBuilderType">The builder type to mark as the default for its model type.</param>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder(Type modelBuilderType)
    {
        Mutate(s => s.UseAsDefaultModelBuilder(modelBuilderType));
        return this;
    }

    /// <summary>Validates the current registrations (see
    /// <see cref="ServiceCollectionExtensions.ValidateXModelBuilderRegistrations"/>); throws on any violation.</summary>
    public DefaultModelBuilderProvider Validate()
    {
        lock (_lock)
        {
            _services.ValidateXModelBuilderRegistrations();
        }
        return this;
    }

    /// <summary>
    /// Configures the <see cref="ModelBuilderOptions"/> (e.g. cultures used for conversion). A
    /// <see langword="null"/> <paramref name="configure"/> is a no-op.
    /// </summary>
    /// <param name="configure">A callback that mutates the options, or <see langword="null"/> to leave them unchanged.</param>
    /// <returns>This provider, to allow call chaining.</returns>
    public DefaultModelBuilderProvider AddOptions(Action<ModelBuilderOptions>? configure = null)
    {
        if (configure != null)
        {
            Mutate(s => s.Configure(configure));
        }
        return this;
    }

    /// <summary>Registers an already-constructed faker instance (e.g. one wired up with its own
    /// seeded Random/Bogus.Faker by hand). For letting the container construct the faker itself
    /// (so it can have its own injected dependencies), use <see cref="AddFaker{TFaker}"/> instead.</summary>
    public DefaultModelBuilderProvider AddFaker(IFaker faker)
    {
        ArgumentNullException.ThrowIfNull(faker);
        Mutate(s =>
        {
            s.AddSingleton(faker.GetType(), faker);
            s.AddSingleton(typeof(IFaker), faker);
        });
        return this;
    }

    /// <summary>Registers <typeparamref name="TFaker"/> for the container to construct itself
    /// (resolving its own constructor dependencies), consistent with the DI-based
    /// <see cref="ServiceCollectionExtensions.AddFaker{TFaker}"/>.</summary>
    public DefaultModelBuilderProvider AddFaker<TFaker>(ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker
    {
        Mutate(s => s.AddFaker<TFaker>(lifetime));
        return this;
    }

    /// <summary>
    /// Escape hatch for registering arbitrary additional services into the internal service
    /// collection - e.g. a dependency that a container-constructed faker (<see cref="AddFaker{TFaker}"/>)
    /// needs injected into its own constructor.
    /// </summary>
    public DefaultModelBuilderProvider AddServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Mutate(configure);
        return this;
    }

    private void Mutate(Action<IServiceCollection> mutate)
    {
        lock (_lock)
        {
            mutate(_services);
            _builtProvider = null;
        }
    }

    private ModelBuilderProvider GetProvider()
    {
        lock (_lock)
        {
            return _builtProvider ??= new ModelBuilderProvider(_services.BuildServiceProvider());
        }
    }
}
