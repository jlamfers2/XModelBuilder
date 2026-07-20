using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using XModelBuilder.Core;

namespace XModelBuilder;

/// <summary>
/// Non-generic base of <see cref="ModelBuilder{TBuilder,TModel}"/> holding the storage that does
/// not depend on the builder/model type parameters: the recorded deep-path settings and constructor
/// arguments. Keeping this state on a shared, non-generic base is what lets the CROSS-CUTTING
/// LAYER (a differently-closed builder for the same model type) seed its settings into
/// another builder via <see cref="SeedFromLayer"/> - the generic type parameters would otherwise
/// make that state inaccessible across sibling closed generics.
/// </summary>
public abstract class ModelBuilderBase
{
    /// <summary>
    /// A recorded lambda deep-path setting: the target expression plus either a fixed value or a
    /// lazy value factory evaluated at build time.
    /// </summary>
    protected sealed class DeepPathExpression
    {
        /// <summary>The lambda selecting the target member (possibly a nested/indexed deep path).</summary>
        public LambdaExpression DeepPath { get; set; } = null!;
        /// <summary>The fixed value to assign, used when <see cref="ValueFactory"/> is not set.</summary>
        public object? Value { get; set; }
        /// <summary>A factory producing the value lazily at build time; takes precedence over <see cref="Value"/>.</summary>
        public Func<object?>? ValueFactory { get; set; }
    }

    /// <summary>
    /// One recorded setting: either a lambda deep-path expression, a single string deep-path/value
    /// pair, or a batch of such pairs (e.g. the rows of a Gherkin table).
    /// </summary>
    protected sealed class DeepPathSetting
    {
        /// <summary>The lambda deep-path setting, when this entry is expression-based.</summary>
        public DeepPathExpression? DeepPathExpression { get; set; }
        /// <summary>A single string deep-path/value pair, when this entry is string-based.</summary>
        public KeyValuePair<string, string?>? DeepPathValue { get; set; }
        /// <summary>A batch of string deep-path/value pairs, when this entry is a batch.</summary>
        public List<KeyValuePair<string, string?>>? DeepPathValues { get; set; }
    }

    private protected sealed class CtorParameterInfo
    {
        public ParameterInfo Parameter { get; set; } = null!;
        public object? Value { get; set; }
        public Func<object?>? ValueFactory { get; set; }

        // Re-converts Value fresh on every call instead of caching the converted result, so a
        // string value (e.g. a faker/token call) is re-evaluated on every Build() - consistent
        // with how deep-path string values already behave, and required for BuildMany to produce
        // varied results for ctor-bound properties.
        public object? GetValue(CultureInfo datetimeCulture, CultureInfo defaultCulture, IModelBuilderProvider xprovider)
        {
            if (ValueFactory != null)
            {
                return ValueFactory();
            }
            if (Value is string stringValue)
            {
                // Always go through ValueConverter, even when the parameter is itself typed
                // string - consistent with deep-path string values, and required for tokens
                // (faker calls, null()/new()/default()) to work on string-typed ctor arguments.
                return ValueConverter.Convert(stringValue, Parameter.ParameterType, datetimeCulture, defaultCulture, xprovider);
            }
            if (Value != null)
            {
                return Value;
            }
            if (Parameter.IsOptional)
            {
                return Parameter.DefaultValue;
            }
            return null; // will probably lead to a construction exception
        }
    }

    private protected readonly List<DeepPathSetting> _deepPathSettingList = [];
    private protected readonly Dictionary<string, CtorParameterInfo> _ctorArguments = new(StringComparer.InvariantCultureIgnoreCase);

    // Seeds this builder with the settings recorded by the cross-cutting-layer builder <paramref name="source"/>,
    // at the LOWEST precedence: its deep-path settings are added before this builder's own (so a later
    // setting on the same target wins), and its constructor arguments are copied in (so a later same-named
    // argument overrides them). Because both builders derive from ModelBuilderBase, the private storage of
    // 'source' is accessible here even though the two are differently-closed generic siblings.
    private protected void SeedFromLayer(ModelBuilderBase source)
    {
        _deepPathSettingList.AddRange(source._deepPathSettingList);
        foreach (var (name, arg) in source._ctorArguments)
        {
            _ctorArguments[name] = arg;
        }
    }
}

/// <summary>
/// Reflection-based base class that turns any model type into a fluent, deterministic test-data
/// builder (Object Mother + Test Data Builder). Derive a concrete builder as
/// <c>class PersonBuilder : ModelBuilder&lt;PersonBuilder, Person&gt;</c> and override
/// <see cref="SetDefaults"/> to seed default values. Members are configured through the typed
/// <c>With</c> lambdas, string deep-paths (<c>"Address.Street"</c>, <c>"Lines[2].Amount"</c>) or
/// <see cref="WithValues"/> (e.g. Gherkin tables); values bound to constructor parameters are routed
/// into construction, the rest are applied afterwards. Values may be literals, lazy factories,
/// nested builders, faker tokens or the special <c>null()</c>/<c>new()</c>/<c>default()</c> tokens.
/// <para>
/// Every build seeds the optional CROSS-CUTTING layer (chapter 5): <see cref="Reset"/> applies the
/// registered cross-cutting layer's settings (lowest precedence) before calling this builder's own
/// <see cref="SetDefaults"/>, so a specific builder is layered ON TOP of it in a single pipeline.
/// </para>
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type itself (CRTP), enabling fluent chaining that returns the derived type.</typeparam>
/// <typeparam name="TModel">The model type this builder builds.</typeparam>
public abstract class ModelBuilder<TBuilder, TModel> : ModelBuilderBase, IModelBuilder<TModel>, IModelBuilder
    where TModel : class
    where TBuilder : ModelBuilder<TBuilder, TModel>
{

    private static readonly ConstructorInfo? _modelCtor = FindModelCtor();
    private static bool _useStandardActivator;
    private static ConstructorInfo? FindModelCtor()
    {
        var ctors = typeof(TModel).GetConstructors();
        if (ctors.Length == 0)
        {
            _useStandardActivator = false;
            return null;
        }
        // ctors is here guaranteed non-empty (the empty case returned above), so First()/ctors[0]
        // never yields null.
        var ctor = ctors.Length > 1 ? ctors.OrderBy(c => c.GetParameters().Length).First() : ctors[0];
        _useStandardActivator = ctor.GetParameters().Length == 0 || ctor.GetParameters().All(p => p.IsOptional);
        return ctor;
    }

    private readonly IModelBuilderProvider _xprovider;
    private readonly ModelBuilderOptions _options;
    private TModel? _extendInstance;

    /// <inheritdoc/>
    public Type ModelType => typeof(TModel);

    /// <summary>
    /// Initializes the builder and applies <see cref="SetDefaults"/> for the first time.
    /// </summary>
    /// <param name="options">The options controlling conversion cultures.</param>
    /// <param name="xprovider">The provider used to resolve nested builders and fakers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="xprovider"/> is <see langword="null"/>.</exception>
    protected ModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(xprovider);
        _options = options.Value;
        _xprovider = xprovider;
        Reset();
    }

    /// <summary>
    /// Clears all configured values, re-applies the optional CROSS-CUTTING layer, then re-applies
    /// this builder's own <see cref="SetDefaults"/>, returning the builder to its initial state.
    /// The cross-cutting layer's settings carry the lowest precedence, so anything set here or later
    /// by the caller overrides them (chapter 5).
    /// </summary>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder Reset()
    {
        _deepPathSettingList.Clear();
        _ctorArguments.Clear();
        ApplyCrossCuttingLayer();
        SetDefaults();
        return (TBuilder)this;
    }

    // Seeds the settings of the registered cross-cutting layer for TModel into this builder, before
    // this builder's own SetDefaults runs. The provider's recursion guard returns null while the
    // cross-cutting layer for TModel is itself being constructed, so it never re-applies itself;
    // ForEmpty likewise resolves with the layer suppressed. A provider that does not support the
    // cross-cutting layer (no ICrossCuttingLayerProvider), or a type with no layer registered, simply
    // applies nothing.
    private void ApplyCrossCuttingLayer()
    {
        if (_xprovider is not ICrossCuttingLayerProvider layerProvider)
        {
            return;
        }
        if (layerProvider.GetCrossCuttingLayer(typeof(TModel)) is ModelBuilderBase layer && !ReferenceEquals(layer, this))
        {
            SeedFromLayer(layer);
        }
    }

    /// <summary>
    /// Builds ONTO an existing <paramref name="instance"/> instead of constructing a new one, and returns
    /// it. Runs the full Build pipeline - including any <see cref="Build"/> override, so derived/computed
    /// members are recomputed - but <see cref="CreateInstance"/> yields <paramref name="instance"/> rather
    /// than a fresh object; the configured <c>With</c>/<c>WithValues</c> values are applied on top.
    /// <para>
    /// This is a one-shot, TERMINAL call just like <see cref="Build"/>, and it does NOT change the
    /// builder's state: you can call <see cref="Build"/> before and after Extend and each still constructs
    /// a brand-new instance as usual. Handy to compose a model over MULTIPLE datasets (e.g. several Gherkin
    /// tables) without cramming everything into one: build the base from the first table, then Extend it
    /// with the next.
    /// </para>
    /// <para>
    /// Everything you configure is applied - regardless of setter/init/ctor/backing-field. Because no
    /// constructor runs, values that would normally be constructor arguments are set directly on the
    /// existing instance (via setter or backing field). Members you do NOT specify keep their current value.
    /// </para>
    /// </summary>
    public TModel Extend(TModel instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _extendInstance = instance;
        try
        {
            return Build();
        }
        finally
        {
            _extendInstance = null;
        }
    }

    /// <summary>
    /// Sets the member selected by <paramref name="memberPath"/> to a fixed value. When the member
    /// maps to a constructor parameter, the value is routed into construction instead of being set
    /// afterwards.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="memberPath">A type-safe expression selecting the target member.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder With<TValue>(Expression<Func<TModel, TValue>> memberPath, TValue? value)
    {
        if(!HandleCtorArgument(memberPath, value, null))
        {
            _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, Value = value } });
        }
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the member selected by <paramref name="getter"/> to a nested model configured through its
    /// own fluent builder.
    /// </summary>
    /// <typeparam name="TValue">The nested member's type.</typeparam>
    /// <param name="getter">A type-safe expression selecting the target member.</param>
    /// <param name="builder">A callback that configures the nested builder for the member.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder)
        where TValue : class
    {
        return With(getter, () => builder(_xprovider.For<TValue>()).Build());
    }

    /// <summary>
    /// Sets the member selected by <paramref name="memberPath"/> to a value produced lazily at build
    /// time. The factory is re-evaluated on every <see cref="Build"/>.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="memberPath">A type-safe expression selecting the target member.</param>
    /// <param name="valueFactory">A factory invoked at build time to produce the value.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder With<TValue>(Expression<Func<TModel, TValue>> memberPath, Func<TValue?> valueFactory)
    {
        if (!HandleCtorArgument(memberPath, null, valueFactory))
        {
            _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, ValueFactory = () => valueFactory() } });
        }
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the member selected by <paramref name="memberPath"/> to a value produced lazily at build
    /// time, with the builder's own <see cref="IModelBuilderProvider"/> passed to the factory.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="memberPath">A type-safe expression selecting the target member.</param>
    /// <param name="valueFactory">A factory, receiving the provider, invoked at build time to produce the value.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder With<TValue>(Expression<Func<TModel, TValue>> memberPath, Func<IModelBuilderProvider, TValue?> valueFactory)
    {
        return With(memberPath, () => valueFactory(_xprovider));
    }

    /// <summary>
    /// Sets the member at the given string deep-path (e.g. <c>"Address.Street"</c> or
    /// <c>"Lines[2].Amount"</c>) to a culture-aware converted value. When the (top-level) member maps
    /// to a constructor parameter, the value is routed into construction instead.
    /// </summary>
    /// <param name="memberPath">The string deep-path to the target member.</param>
    /// <param name="value">The raw text value to convert and assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder With(string memberPath, string? value)
    {
        if (!HandleCtorArgument(memberPath, value))
        {
            _deepPathSettingList.Add(new DeepPathSetting { DeepPathValue = new KeyValuePair<string, string?>(memberPath, value) });
        }
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the member selected by <paramref name="memberPath"/> to the result of building the model
    /// builder registered under <see cref="ModelBuilderAttribute"/> name <paramref name="builderName"/>
    /// for <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="memberPath">A type-safe expression selecting the target member.</param>
    /// <param name="builderName">The registered name of the builder to use for the member's value.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder WithBuilder<TValue>(Expression<Func<TModel, TValue>> memberPath, string builderName)
        where TValue : class?
    {
        // 'class?' (not 'class') so a nullable-annotated member does not warn at the call site; the
        // build goes through the non-generic For(Type, name) to avoid a nullable type argument on the
        // 'class'-constrained For<TModel>(name).
        return With(memberPath, () => (TValue?)_xprovider.For(typeof(TValue), builderName).Build());
    }

    /// <summary>
    /// Sets the member selected by <paramref name="memberPath"/> to a fresh instance built through the
    /// DEFAULT builder for <typeparamref name="TValue"/> (so that type's <see cref="SetDefaults"/>
    /// runs). This is the strongly-typed counterpart of the <c>"default()"</c> string token for a
    /// complex member type - and, like <see cref="WithBuilder{TValue}(Expression{Func{TModel,TValue}}, string)"/>
    /// versus a named-builder reference, a refactor-safe alternative to <c>With("Member", "default()")</c>.
    /// The value is produced lazily at build time and, when the member maps to a constructor parameter,
    /// routed into construction. A <see cref="string"/>-typed member yields <see langword="null"/>,
    /// matching the <c>"default()"</c> token.
    /// </summary>
    /// <typeparam name="TValue">The nested member's type.</typeparam>
    /// <param name="memberPath">A type-safe expression selecting the target member.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder WithDefault<TValue>(Expression<Func<TModel, TValue>> memberPath)
        where TValue : class?
    {
        // Mirrors the "default()" token (chapter 10): for a complex reference type, build via the
        // default builder; for string it is null (the token's string behavior). The constraint is
        // 'class?' (not 'class') so a nullable-annotated member (e.g. Address?) does not warn at the
        // call site; the build goes through the non-generic For(Type) to avoid a nullable type
        // argument on the 'class'-constrained For<TModel>().
        return With(memberPath, () =>
        {
            if (typeof(TValue) == typeof(string))
            {
                return null;
            }
            return (TValue?)_xprovider.For(typeof(TValue)).Build();
        });
    }

    /// <summary>
    /// Sets multiple members at once from a sequence of deep-path/value pairs (e.g. the rows of a
    /// Gherkin table). Values mapping to constructor parameters are routed into construction; the rest
    /// are applied afterwards.
    /// </summary>
    /// <param name="values">The member deep-path/value pairs to apply.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    public TBuilder WithValues(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var setterValues = values.Where(v => !HandleCtorArgument(v.Key, v.Value)).ToList();
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathValues = [.. setterValues] });
        return (TBuilder)this;
    }

    /// <summary>
    /// Constructs a new <typeparamref name="TModel"/> (routing configured constructor values into
    /// construction) and applies all remaining configured values to it. Override to add
    /// computed/derived logic after the base build.
    /// </summary>
    /// <returns>The built model.</returns>
    public virtual TModel Build()
    {
        // Turn a cyclic build (a type that, via WithDefault/WithBuilder/"default()"/named-builder
        // values or auto-vivified nested paths, ends up building itself) into a catchable exception
        // instead of an unrecoverable StackOverflowException. Every nested build funnels through a
        // builder's Build(), so this single guard covers all of those mechanisms.
        BuildReentrancyGuard.Enter(typeof(TModel));
        try
        {
            var model = CreateInstance();

            foreach (var setting in _deepPathSettingList)
            {
                ApplyDeepPathSetting(model, setting);
            }

            return model;
        }
        finally
        {
            BuildReentrancyGuard.Exit();
        }
    }

    /// <summary>
    /// Sets a member on an already-constructed <paramref name="model"/> using the SAME member
    /// resolution as the deep-path setters: a property setter, an init-only setter, or - when there is
    /// no accessible setter - the backing field. Supports nested paths and indexers too
    /// (<c>x =&gt; x.Lines[0].Amount</c>).
    /// <para>
    /// Intended for a <see cref="Build"/> override to apply COMPUTED/DERIVED values after the table/With
    /// values are in - including on read-only/init-only members - e.g. a cross-field default:
    /// <code>
    /// public override Product Build()
    /// {
    ///     var product = base.Build();
    ///     if (product.PriceWithVat is null)
    ///         SetMember(product, x =&gt; x.PriceWithVat, product.Price * 1.21m);
    ///     return product;
    /// }
    /// </code>
    /// (A constructor-only value that is itself derived from another constructor argument must instead
    /// be produced before construction - override <see cref="CreateInstance"/> for that rare case.)
    /// </para>
    /// </summary>
    protected void SetMember<TValue>(TModel model, Expression<Func<TModel, TValue>> member, TValue? value)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(member);
        LambdaPathSetter.SetMemberValueByLambdaUntyped(model, member, value, _xprovider);
    }

    #region IModelBuilder<TModel>

    IModelBuilder<TModel> IModelBuilder<TModel>.Reset()
    {
        return Reset();
    }

    TModel IModelBuilder<TModel>.Extend(TModel instance)
    {
        return Extend(instance);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.WithValues(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return WithValues(values);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.With<TValue>(Expression<Func<TModel, TValue>> getter, TValue? value) where TValue : default
    {
        return With(getter, value);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.With(string memberPath, string value)
    {
        return With(memberPath, value);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.With<TValue>(Expression<Func<TModel, TValue>> getter, Func<TValue?> valueFactory) where TValue : default
    {
        return With(getter, valueFactory);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilderProvider, TValue?> valueFactory) where TValue : default
    {
        return With(getter, valueFactory);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.WithBuilder<TValue>(Expression<Func<TModel, TValue>> getter, string builderName)
    {
        return WithBuilder(getter, builderName);
    }

    IModelBuilder<TModel> IModelBuilder<TModel>.WithDefault<TValue>(Expression<Func<TModel, TValue>> getter)
    {
        return WithDefault(getter);
    }
    #endregion

    #region IModelBuilder
    object IModelBuilder.Build()
    {
        return Build();
    }

    IModelBuilder IModelBuilder.Reset()
    {
        return Reset();
    }

    object IModelBuilder.Extend(object instance)
    {
        return Extend((TModel)instance);
    }

    IModelBuilder IModelBuilder.WithValues(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return WithValues(values);
    }

    IModelBuilder IModelBuilder.With(LambdaExpression memberPath, object? value)
    {
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, Value = value } });
        return this;
    }

    IModelBuilder IModelBuilder.With(LambdaExpression memberPath, Func<object?> valueFactory)
    {
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, ValueFactory = () => valueFactory() } });
        return this;
    }

    IModelBuilder IModelBuilder.With(LambdaExpression memberPath, Func<IModelBuilderProvider, object?> valueFactory)
    {
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, ValueFactory = () => valueFactory(_xprovider) } });
        return this;
    }

    IModelBuilder IModelBuilder.With(string memberPath, string value)
    {
        return With(memberPath, value);
    }

    IModelBuilder IModelBuilder.WithBuilder(LambdaExpression memberPath, string builderName)
    {
        var valueType = memberPath.ReturnType;
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, ValueFactory = () => _xprovider.For(valueType, builderName).Build() } });
        return this;
    }

    IModelBuilder IModelBuilder.WithDefault(LambdaExpression memberPath)
    {
        var valueType = memberPath.ReturnType;
        _deepPathSettingList.Add(new DeepPathSetting { DeepPathExpression = new DeepPathExpression { DeepPath = memberPath, ValueFactory = () => valueType == typeof(string) ? null : _xprovider.For(valueType).Build() } });
        return this;
    }
    #endregion

    protected abstract void SetDefaults();

    protected virtual TModel CreateInstance()
    {
        // Extend mode: return the supplied instance instead of creating a new one. Because no
        // constructor runs, values that would otherwise be ctor arguments are still applied here -
        // via the setter or the backing field - so there is no asymmetry with regular member values.
        if (_extendInstance != null)
        {
            ApplyCtorArgumentsAsMembers(_extendInstance);
            return _extendInstance;
        }

        if (_useStandardActivator)
        {
            return (TModel)Activator.CreateInstance(typeof(TModel))!;
        }

        if (_modelCtor == null)
        {
            return (TModel)Instantiator.CreateInstance(typeof(TModel));
        }

        var args = _modelCtor
            .GetParameters()
            .Select(p =>
                _ctorArguments
                .TryGetValue(p.Name!, out var arg)
                    ? arg.GetValue(_options.DateTimeCulture, _options.DefaultCulture, _xprovider)
                    : p.GetParameterDefaultValueOrNull())
            .ToArray();

        return (TModel)_modelCtor.Invoke(args);
    }

    protected virtual void ApplyDeepPathSetting(TModel model, DeepPathSetting setting)
    {
        if (setting.DeepPathExpression != null)
        {
            ApplyDeepPathExpression(model, setting.DeepPathExpression);
        }
        else if (setting.DeepPathValue != null)
        {
            ApplyDeepPathValue(model, setting.DeepPathValue.Value.Key, setting.DeepPathValue.Value.Value);
        }
        else if (setting.DeepPathValues != null)
        {
            foreach (var kvp in setting.DeepPathValues)
            {
                ApplyDeepPathValue(model, kvp.Key, kvp.Value);
            }
        }
        else
        {
            throw new ArgumentException($"Any {nameof(DeepPathSetting)} property must not be null", nameof(setting));
        }
    }
    protected void ApplyDeepPathValue(TModel model, string deepPath, string? value)
    {
        if (model == null)
        {
            return;
        }

        var type = model.GetType();

        if (deepPath.Contains('.') || deepPath.Contains('['))
        {
            StringPathSetter.SetMemberValueByString(model, deepPath, value, _options.DateTimeCulture, _options.DefaultCulture, _xprovider);
        }
        else
        {

            if (!type.TryGetWritableMember(deepPath, out var member))
            {
                throw new InvalidOperationException($"Unable to set model's member {deepPath} for type {typeof(TModel).GetFriendlyName(true)}");
            }

            var converted = ValueConverter.Convert(value, member.GetMemberType(), _options.DateTimeCulture, _options.DefaultCulture, _xprovider);

            member.SetMemberValue(model, converted);
        }
    }
    protected void ApplyDeepPathExpression(TModel model, DeepPathExpression deepPathExpression)
    {
        if (model == null)
        {
            return;
        }
        if (deepPathExpression.ValueFactory != null)
        {
            LambdaPathSetter.SetMemberValueByLambdaUntyped(model, deepPathExpression.DeepPath, deepPathExpression.ValueFactory(), _xprovider);
        }
        else
        {
            LambdaPathSetter.SetMemberValueByLambdaUntyped(model, deepPathExpression.DeepPath, deepPathExpression.Value, _xprovider);
        }
    }
    // In extend mode, apply the ctor-configured values onto the existing instance anyway (via the setter
    // or the backing field). Only what is explicitly supplied is set; unspecified ctor-only members keep
    // their existing value. A ctor parameter without a corresponding member is skipped.
    private void ApplyCtorArgumentsAsMembers(TModel instance)
    {
        foreach (var (name, arg) in _ctorArguments)
        {
            if (typeof(TModel).TryGetWritableMember(name, out var member))
            {
                var value = arg.GetValue(_options.DateTimeCulture, _options.DefaultCulture, _xprovider);
                member.SetMemberValue(instance, value);
            }
        }
    }

    private bool HandleCtorArgument<TValue>(Expression<Func<TModel, TValue>> memberPath, object? value, Func<TValue?>? valueFactory)
    {
        var name = default(string?);
        if (!_useStandardActivator && _modelCtor != null && (name = memberPath.GetShallowPropertyName()) != null)
        {
            var parameter = _modelCtor.GetParameters().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (parameter != null)
            {
                _ctorArguments[name] = new CtorParameterInfo { Parameter = parameter, Value = value, ValueFactory = valueFactory != null ? () => valueFactory() : null  };
                return true;
            }
        }
        return false;
    }
    private bool HandleCtorArgument(string memberPath, string? value)
    {
        if (_useStandardActivator || _modelCtor == null)
        {
            return false;
        }

        var idx = memberPath.IndexOf('.');
        var name = idx == -1 ? memberPath : memberPath[..idx];

        var parameter = _modelCtor.GetParameters().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (parameter == null)
        {
            return false;
        }

        _ctorArguments[name] = new CtorParameterInfo { Parameter = parameter, Value = value, ValueFactory = null };
        return true;
    }

}
