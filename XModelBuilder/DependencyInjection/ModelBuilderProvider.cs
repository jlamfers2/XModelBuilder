using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;
using XModelBuilder.Core;
using XModelBuilder.Default;

namespace XModelBuilder.DependencyInjection
{
    /// <summary>
    /// The DI-backed <see cref="IModelBuilderProvider"/>: the single place with real resolution logic.
    /// Resolves builders and fakers from the wrapped <see cref="IServiceProvider"/>, enforcing that a
    /// default is explicitly configured (order-independently) when multiple builders exist for a model
    /// type, and supports faker-token invocation via <see cref="IFakerInvocationSource"/>.
    /// </summary>
    /// <param name="services">The service provider the builders, fakers and options are resolved from.</param>
    public class ModelBuilderProvider(IServiceProvider services) : IModelBuilderProvider, IFakerInvocationSource
    {
        private readonly IServiceProvider _services = services;

        /// <inheritdoc/>
        public IModelBuilder For(Type modelType)
        {
            var resolveType = typeof(IModelBuilder<>).MakeGenericType(modelType);
            var candidates = _services.GetServices(resolveType).Cast<IModelBuilder>().ToList();

            if (candidates.Count == 0)
            {
                var fallback = _services.GetKeyedService(resolveType, "default")
                    ?? throw new KeyNotFoundException($"Could not resolve {resolveType.GetFriendlyName(true)}");
                return (IModelBuilder)fallback;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            // Multiple builders for this model type: the default must be configured explicitly and
            // order-independently via UseAsDefaultModelBuilder<TBuilder>() - there is no implicit
            // "last registered wins". Resolve by name (For<T>(name)) to pick a specific one.
            var defaultBuilderType = (_services.GetService(typeof(ModelBuilderDefaults)) as ModelBuilderDefaults)
                ?.GetBuilderType(modelType);

            if (defaultBuilderType is null)
            {
                throw new InvalidOperationException(
                    $"Multiple model builders are registered for {modelType.GetFriendlyName(true)} but no default is configured. " +
                    $"Call UseAsDefaultModelBuilder<TBuilder>() to pick one, or resolve a specific builder by name via For(..., name).");
            }

            return candidates.FirstOrDefault(c => c.GetType() == defaultBuilderType)
                ?? throw new InvalidOperationException(
                    $"The configured default model builder '{defaultBuilderType.GetFriendlyName()}' for {modelType.GetFriendlyName(true)} is not among the registered builders.");
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
            var candidates = _services.GetServices(resolveType).Cast<IModelBuilder>().ToList();

            // Names are unique per model type (enforced by ValidateXModelBuilderRegistrations), so
            // this is order-independent.
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
        public IModelBuilder<TModel> NewDefaultModelBuilder<TModel>() where TModel : class
        {
            // Construct the built-in DefaultModelBuilder<TModel> directly - NOT the keyed "default"
            // registration, which could have been replaced by an open-generic that sets fields.
            var options = _services.GetRequiredService<IOptions<ModelBuilderOptions>>();
            return new DefaultModelBuilder<TModel>(options, this);
        }

        /// <inheritdoc/>
        public TFaker Faker<TFaker>() where TFaker : IFaker
        {
            return _services.GetService<TFaker>()
                ?? throw new KeyNotFoundException($"No faker of type '{typeof(TFaker).GetFriendlyName(true)}' is registered.");
        }

        object? IFakerInvocationSource.InvokeFaker(string name, object?[] args, Type targetType, CultureInfo culture)
        {
            var fakers = _services.GetServices<IFaker>().ToList();
            return FakerInvoker.Invoke(fakers, name, args, targetType, culture, this, _services);
        }
    }
}
