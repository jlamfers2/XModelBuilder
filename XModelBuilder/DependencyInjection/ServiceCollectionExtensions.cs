using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using XModelBuilder.Core;
using XModelBuilder.Default;

namespace XModelBuilder.DependencyInjection
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering XModelBuilder with a DI
    /// container: the core services and options (<see cref="AddXModelBuilder"/>), individual or
    /// assembly-scanned builders, the optional cross-cutting layer
    /// (<see cref="AddCrossCuttingModelBuilder"/>), fakers, and a post-registration validation pass.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the core XModelBuilder services (<see cref="IModelBuilderProvider"/>, options and
        /// the open-generic fallback builder) and the chosen isolation mode. Any faker/seeder
        /// registrations deferred before the isolation was known are flushed here, so call order
        /// relative to <c>AddXFaker</c>/<c>AddBogusFaker</c> does not matter.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional callback to configure <see cref="ModelBuilderOptions"/>.</param>
        /// <param name="isolation">The isolation mode determining the provider/faker service lifetime.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        public static IServiceCollection AddXModelBuilder(
                this IServiceCollection services,
                Action<ModelBuilderOptions>? configure = null,
                XModelBuilderIsolation isolation = XModelBuilderIsolation.Shared)
        {
            if (configure is not null)
            {
                services.Configure(configure);
            }
            else
            {
                services.AddOptions<ModelBuilderOptions>();
            }

            var lifetime = XModelBuilderIsolationState.ToLifetime(isolation);

            var state = services.GetOrAddIsolationState();
            state.Isolation = isolation;

            // The fixed, sealed no-op base layer. Not user-replaceable: cross-cutting defaults go in a
            // SEPARATE slot via AddCrossCuttingModelBuilder.
            services.AddKeyedTransient(typeof(IModelBuilder<>), ModelBuilderProvider.DefaultBaseKey, typeof(DefaultModelBuilder<>));
            services.TryAdd(new ServiceDescriptor(typeof(IModelBuilderProvider), typeof(ModelBuilderProvider), lifetime));

            // Flush any faker/seeder registrations that arrived before the isolation was known, so
            // the call order of AddXModelBuilder vs AddXFaker/AddBogusFaker does not matter.
            foreach (var register in state.PendingRegistrations)
            {
                register(services, lifetime);
            }
            state.PendingRegistrations.Clear();

            return services;
        }

        /// <summary>
        /// Registers services (a faker and its seeded dependencies) whose lifetime must follow the
        /// configured <see cref="XModelBuilderIsolation"/>. ORDER-INDEPENDENT: if the isolation is
        /// already known (AddXModelBuilder ran first) the services are registered immediately with the
        /// matching lifetime; otherwise the registration is deferred and flushed when AddXModelBuilder
        /// sets the isolation. Used by <c>AddXFaker</c>/<c>AddBogusFaker</c>.
        /// </summary>
        public static IServiceCollection AddIsolatedXModelBuilderServices(
                        this IServiceCollection services,
                        Action<IServiceCollection, ServiceLifetime> register)
        {
            ArgumentNullException.ThrowIfNull(register);

            var state = services.GetOrAddIsolationState();
            if (state.Isolation is { } isolation)
            {
                register(services, XModelBuilderIsolationState.ToLifetime(isolation));
            }
            else
            {
                state.PendingRegistrations.Add(register);
            }
            return services;
        }

        /// <summary>
        /// Registers a single dedicated builder for its model type (derived from the builder's
        /// <see cref="IModelBuilder{T}"/> interface).
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="modelBuilderType">The builder type to register.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="modelBuilderType"/> does not implement <see cref="IModelBuilder{T}"/>.</exception>
        public static IServiceCollection AddModelBuilder(
                        this IServiceCollection services,
                        Type modelBuilderType)
        {
            var serviceType = modelBuilderType.GetModelBuilderInterfaceType();

            if (serviceType == null)
            {
                throw new ArgumentException($"Invalid model builder type: {modelBuilderType.GetFriendlyName()}", nameof(modelBuilderType));
            }

            services.TryAddTransient(modelBuilderType, modelBuilderType);
            services.AddTransient(serviceType, modelBuilderType);
            return services;
        }

        /// <summary>
        /// Registers a single dedicated builder <typeparamref name="TModelBuilder"/> for its model type.
        /// </summary>
        /// <typeparam name="TModelBuilder">The builder type to register.</typeparam>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        public static IServiceCollection AddModelBuilder<TModelBuilder>(
                        this IServiceCollection services) where TModelBuilder: IModelBuilder
        {
            return services.AddModelBuilder(typeof(TModelBuilder));
        }

        /// <summary>
        /// Registers every non-abstract, non-generic <see cref="IModelBuilder"/> implementation found
        /// in the given assembly.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="assembly">The assembly to scan for builders.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        public static IServiceCollection AddModelBuildersFromAssembly(
                        this IServiceCollection services,
                        Assembly assembly)
        {
            foreach(var type in assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericType && typeof(IModelBuilder).IsAssignableFrom(t))
                )
            {
                services.AddModelBuilder(type);
            }
            return services;
        }

        /// <summary>
        /// Registers every non-abstract, non-generic <see cref="IModelBuilder"/> implementation found
        /// across the whole AppDomain (via the <see cref="AssemblyScanner"/>). Handy for larger apps
        /// with many model builders spread over several assemblies. Each builder still needs a unique
        /// <c>[ModelBuilder(name)]</c>; a model type may have several builders (addressed by name/type,
        /// with no "default among them" to configure). Verify the whole set with
        /// <see cref="ValidateXModelBuilderRegistrations"/>.
        /// </summary>
        public static IServiceCollection AddModelBuildersFromAssemblies(
                        this IServiceCollection services)
        {
            foreach (var type in AssemblyScanner.GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericType && typeof(IModelBuilder).IsAssignableFrom(t))
                )
            {
                services.AddModelBuilder(type);
            }
            return services;
        }

        /// <summary>
        /// Registers the optional CROSS-CUTTING layer: an open-generic builder that is applied to EVERY
        /// build (on top of the fixed <see cref="DefaultModelBuilder{TModel}"/> base and under any
        /// specific builder), providing cross-cutting defaults (e.g. a deterministic Guid <c>Id</c>) for
        /// every model type. Its settings carry the LOWEST precedence, so anything a specific builder or
        /// the caller sets overrides them; <c>ForEmpty</c> opts out of it entirely. The type must be an
        /// open generic <see cref="IModelBuilder"/> with a single type parameter (e.g. <c>EntityDefaults&lt;&gt;</c>).
        /// Registering again replaces the layer (last-wins). See README chapter 5.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="modelBuilderType">The open-generic builder type to register as the cross-cutting layer.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="modelBuilderType"/> is not a suitable open-generic builder type.</exception>
        public static IServiceCollection AddCrossCuttingModelBuilder(
                        this IServiceCollection services,
                        Type modelBuilderType)
        {
            if (modelBuilderType.IsAbstract || !(typeof(IModelBuilder).IsAssignableFrom(modelBuilderType)) || !modelBuilderType.IsGenericType || modelBuilderType.GetGenericArguments().Length != 1)
            {
                throw new ArgumentException($"Invalid model builder type: {modelBuilderType.GetFriendlyName()}", nameof(modelBuilderType));
            }
            services.AddKeyedTransient(typeof(IModelBuilder<>), ModelBuilderProvider.CrossCuttingLayerKey, modelBuilderType);
            return services;
        }

        /// <summary>
        /// Registers a faker type so the container constructs it (resolving its own dependencies). The
        /// concrete type is registered directly and also forwarded as <see cref="IFaker"/> to the SAME
        /// instance/scope, so direct resolution and token dispatch always agree.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="fakerType">The faker type to register.</param>
        /// <param name="lifetime">The service lifetime for the faker.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fakerType"/> is abstract or does not implement <see cref="IFaker"/>.</exception>
        public static IServiceCollection AddFaker(
                        this IServiceCollection services,
                        Type fakerType,
                        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            if (fakerType.IsAbstract || !typeof(IFaker).IsAssignableFrom(fakerType))
            {
                throw new ArgumentException($"Invalid faker type: {fakerType.GetFriendlyName()}", nameof(fakerType));
            }

            // Register the concrete type itself (so it can be resolved directly via constructor
            // injection or IModelBuilderProvider.Faker<TFaker>()), and forward the IFaker
            // registration (used by the dynamic "name(args)" token dispatch) to that SAME
            // instance/scope, so both resolution paths always agree.
            services.Add(new ServiceDescriptor(fakerType, fakerType, lifetime));
            services.Add(new ServiceDescriptor(typeof(IFaker), sp => sp.GetRequiredService(fakerType), lifetime));
            return services;
        }

        /// <summary>
        /// Registers a faker <typeparamref name="TFaker"/> so the container constructs it (resolving its
        /// own dependencies).
        /// </summary>
        /// <typeparam name="TFaker">The faker type to register.</typeparam>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="lifetime">The service lifetime for the faker.</param>
        /// <returns>The same service collection, to allow call chaining.</returns>
        public static IServiceCollection AddFaker<TFaker>(
                        this IServiceCollection services,
                        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker
        {
            return services.AddFaker(typeof(TFaker), lifetime);
        }

        /// <summary>
        /// Validates, after all registrations are in place, that the model-builder registrations obey
        /// the resolution rules: every builder carries a <c>[ModelBuilder(name)]</c> and names are
        /// unique per model type. Throws an <see cref="InvalidOperationException"/> listing ALL
        /// violations at once.
        /// </summary>
        public static IServiceCollection ValidateXModelBuilderRegistrations(this IServiceCollection services)
        {
            var groups = services
                .Where(d => d.ServiceType.IsConstructedGenericType
                            && d.ServiceType.GetGenericTypeDefinition() == typeof(IModelBuilder<>)
                            && d.ImplementationType is not null)
                .GroupBy(d => d.ServiceType.GetGenericArguments()[0]);

            var errors = new List<string>();

            // The provider lifetime must match the configured isolation; a mismatch means
            // AddXModelBuilder was called more than once with a different isolation.
            if (services.FirstOrDefault(d => d.ServiceType == typeof(XModelBuilderIsolationState))?.ImplementationInstance is XModelBuilderIsolationState state
                && state.Isolation is { } isolation
                && services.FirstOrDefault(d => d.ServiceType == typeof(IModelBuilderProvider)) is { } providerDescriptor
                && providerDescriptor.Lifetime != XModelBuilderIsolationState.ToLifetime(isolation))
            {
                errors.Add($"The IModelBuilderProvider lifetime ({providerDescriptor.Lifetime}) does not match the configured isolation ({isolation}). Was AddXModelBuilder called more than once with a different isolation?");
            }

            foreach (var group in groups)
            {
                var modelType = group.Key;
                var builderTypes = group.Select(d => d.ImplementationType!).Distinct().ToList();

                foreach (var unnamed in builderTypes.Where(t => string.IsNullOrWhiteSpace(t.GetModelBuilderName())))
                {
                    errors.Add($"Model builder '{unnamed.GetFriendlyName()}' for {modelType.GetFriendlyName()} has no [ModelBuilder(name)] attribute.");
                }

                foreach (var dupe in builderTypes
                    .Select(t => t.GetModelBuilderName())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key))
                {
                    errors.Add($"Model builder name '{dupe}' is registered more than once for {modelType.GetFriendlyName()}.");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid XModelBuilder registrations:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
            }

            return services;
        }

        private static XModelBuilderIsolationState GetOrAddIsolationState(this IServiceCollection services)
        {
            if (services.FirstOrDefault(d => d.ServiceType == typeof(XModelBuilderIsolationState))?.ImplementationInstance is XModelBuilderIsolationState existing)
            {
                return existing;
            }

            var created = new XModelBuilderIsolationState();
            services.AddSingleton(created);
            return created;
        }

        private static Type? GetModelBuilderInterfaceType(this Type modelBuilderType)
        {
            var interfaceType = modelBuilderType
                .GetInterfaces()
                .SingleOrDefault(i => i.GetGenericTypeDefinitionOrNull() == typeof(IModelBuilder<>));

            return interfaceType == null ? null : typeof(IModelBuilder<>).MakeGenericType(interfaceType.GetGenericArguments()[0]);
        }

    }
}
