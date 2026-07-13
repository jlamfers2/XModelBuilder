using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using XModelBuilder.Core;
using XModelBuilder.Default;

namespace XModelBuilder.DependencyInjection
{
    /// <summary>
    /// The DI-backed <see cref="IModelBuilderProvider"/>: the single place with real resolution logic.
    /// Resolves builders and fakers from the wrapped <see cref="IServiceProvider"/>. Every build starts
    /// from the fixed, sealed <see cref="DefaultModelBuilder{TModel}"/> BASE (keyed <see cref="DefaultBaseKey"/>);
    /// the optional CROSS-CUTTING layer (keyed <see cref="CrossCuttingLayerKey"/>, registered via
    /// <c>AddCrossCuttingModelBuilder</c>) is seeded into every build; named/typed builders layer on top.
    /// Also implements <see cref="ICrossCuttingLayerProvider"/> (so a builder can seed the cross-cutting
    /// layer on <c>Reset()</c>) and <see cref="IFakerInvocationSource"/> (faker-token invocation).
    /// </summary>
    /// <param name="services">The service provider the builders, fakers and options are resolved from.</param>
    public class ModelBuilderProvider(IServiceProvider services) : IModelBuilderProvider, IFakerInvocationSource, ICrossCuttingLayerProvider
    {
        /// <summary>The keyed-service key of the fixed, sealed <see cref="DefaultModelBuilder{TModel}"/> base layer.</summary>
        internal const string DefaultBaseKey = "default";

        /// <summary>The keyed-service key of the optional cross-cutting layer registered via <c>AddCrossCuttingModelBuilder</c>.</summary>
        internal const string CrossCuttingLayerKey = "crosscutting";

        private readonly IServiceProvider _services = services;

        // The model types whose cross-cutting layer is currently being constructed on this thread. While
        // a type is in this set, GetCrossCuttingLayer returns null for it, so the cross-cutting layer
        // never re-applies itself (recursion guard), and ForEmpty can construct a pristine base instance
        // with the layer suppressed. Thread-static because a shared/singleton provider may be used from
        // several threads, while each construction is synchronous.
        [ThreadStatic]
        private static HashSet<Type>? _suppressedCrossCuttingTypes;

        /// <inheritdoc/>
        public IModelBuilder For(Type modelType)
        {
            // For<T>() (no name) is ALWAYS the sealed base + the cross-cutting layer - never a
            // type-specific builder. Constructing the base runs its Reset, which seeds the cross-cutting
            // layer automatically.
            var resolveType = typeof(IModelBuilder<>).MakeGenericType(modelType);
            return _services.GetKeyedService(resolveType, DefaultBaseKey) as IModelBuilder
                ?? throw new KeyNotFoundException(
                    $"No base model builder is registered for {resolveType.GetFriendlyName(true)}. Was AddXModelBuilder() called?");
        }

        /// <inheritdoc/>
        public IModelBuilder<TModel> For<TModel>() where TModel : class
        {
            return (IModelBuilder<TModel>) For(typeof(TModel));
        }

        /// <inheritdoc/>
        public IModelBuilder For(Type modelType, string name)
        {
            var resolveType = typeof(IModelBuilder<>).MakeGenericType(modelType);
            var candidates = _services.GetServices(resolveType).Cast<IModelBuilder>();

            // Names are unique per model type (enforced by ValidateXModelBuilderRegistrations), so
            // this is order-independent. Each candidate is constructed with the cross-cutting layer
            // already seeded in (via its Reset), so the returned builder is "cross-cutting + this specific".
            foreach (var candidate in candidates)
            {
                if (candidate.GetType().HasModelBuilderName(name))
                {
                    return candidate;
                }
            }

            throw new KeyNotFoundException($"No model builder named '{name}' is registered for {modelType.GetFriendlyName(true)}.");
        }

        /// <inheritdoc/>
        public IModelBuilder<TModel> For<TModel>(string name) where TModel : class
        {
            return (IModelBuilder<TModel>)For(typeof(TModel), name);
        }

        /// <inheritdoc/>
        public IModelBuilder Use(Type modelBuilderType)
        {
            var modelBuilder = _services.GetService(modelBuilderType)
                ?? throw new KeyNotFoundException($"Could not resolve {modelBuilderType.GetFriendlyName(true)}");
            return (IModelBuilder)modelBuilder;
        }

        /// <inheritdoc/>
        public TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder
        {
            var modelBuilder = _services.GetService(typeof(TModelBuilder))
                ?? throw new KeyNotFoundException($"Could not resolve {typeof(TModelBuilder).GetFriendlyName(true)}");
            return (TModelBuilder)modelBuilder;
        }

        /// <inheritdoc/>
        public IModelBuilder<TModel> ForEmpty<TModel>() where TModel : class
        {
            // Construct the sealed DefaultModelBuilder<TModel> base directly, with the cross-cutting layer
            // for this type suppressed, so the instance is truly pristine (no cross-cutting defaults).
            var options = _services.GetRequiredService<IOptions<ModelBuilderOptions>>();
            var set = _suppressedCrossCuttingTypes ??= [];
            var added = set.Add(typeof(TModel));
            try
            {
                return new DefaultModelBuilder<TModel>(options, this);
            }
            finally
            {
                if (added)
                {
                    set.Remove(typeof(TModel));
                }
            }
        }

        /// <inheritdoc/>
        public TFaker Faker<TFaker>() where TFaker : IFaker
        {
            return _services.GetService<TFaker>()
                ?? throw new KeyNotFoundException($"No faker of type '{typeof(TFaker).GetFriendlyName(true)}' is registered.");
        }

        IModelBuilder? ICrossCuttingLayerProvider.GetCrossCuttingLayer(Type modelType)
        {
            // While the cross-cutting layer for this type is being constructed (or ForEmpty suppressed it),
            // it must not be applied.
            if (_suppressedCrossCuttingTypes?.Contains(modelType) == true)
            {
                return null;
            }
            return ResolveCrossCuttingLayer(modelType);
        }

        // Resolves the keyed cross-cutting layer for the model type, guarding against re-entrancy for the
        // same type (the cross-cutting builder's own Reset asks for the layer again during construction).
        // Returns null when no cross-cutting layer is registered.
        private IModelBuilder? ResolveCrossCuttingLayer(Type modelType)
        {
            var resolveType = typeof(IModelBuilder<>).MakeGenericType(modelType);
            var set = _suppressedCrossCuttingTypes ??= [];
            if (!set.Add(modelType))
            {
                return null;
            }
            try
            {
                return _services.GetKeyedService(resolveType, CrossCuttingLayerKey) as IModelBuilder;
            }
            finally
            {
                set.Remove(modelType);
            }
        }

        object? IFakerInvocationSource.InvokeFaker(string name, object?[] args, Type targetType, CultureInfo culture)
        {
            var fakers = _services.GetServices<IFaker>().ToList();
            return FakerInvoker.Invoke(fakers, name, args, targetType, culture, this, _services);
        }
    }
}
