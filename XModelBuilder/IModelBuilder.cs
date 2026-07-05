using System.Linq.Expressions;

namespace XModelBuilder;

/// <summary>
/// Non-generic, weakly-typed fluent builder contract. Configures member values via lambda or string
/// deep-paths and materializes the model with <see cref="Build"/>. This is the reflection-friendly
/// counterpart to <see cref="IModelBuilder{TModel}"/>, used where the model type is only known at runtime.
/// </summary>
public interface IModelBuilder
{
    /// <summary>The model type this builder builds.</summary>
    Type ModelType { get; }

    /// <summary>
    /// Clears any configured values, returning the builder to its initial state.
    /// </summary>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder Reset();

    /// <summary>
    /// Builds onto an existing <paramref name="instance"/> instead of constructing a new one, and returns
    /// it. A one-shot terminal call like <see cref="Build"/> that does not change the builder's state, so
    /// Build still constructs a new instance before and after. Lets you compose a model over multiple
    /// datasets.
    /// </summary>
    object Extend(object instance);

    /// <summary>
    /// Sets the member at the given deep-path to a fixed value.
    /// </summary>
    /// <param name="memberPath">A member-access lambda describing the target member.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder With(LambdaExpression memberPath, object? value);

    /// <summary>
    /// Sets the member at the given deep-path to a value produced lazily at build time.
    /// </summary>
    /// <param name="memberPath">A member-access lambda describing the target member.</param>
    /// <param name="valueFactory">A factory invoked at build time to produce the value.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder With(LambdaExpression memberPath, Func<object?> valueFactory);

    /// <summary>
    /// Like <see cref="With(LambdaExpression, Func{object})"/>, but the factory receives the
    /// builder's own <see cref="IModelBuilderProvider"/> - guaranteed to be the correct one for
    /// this builder, even if the factory is a reusable function not written in a scope where the
    /// "right" provider is otherwise available (e.g. a shared faker-style helper used across
    /// multiple DI scopes/tests).
    /// </summary>
    IModelBuilder With(LambdaExpression memberPath, Func<IModelBuilderProvider, object?> valueFactory);

    /// <summary>
    /// Sets the member at the given string deep-path (e.g. <c>"Address.Street"</c> or
    /// <c>"Lines[2].Amount"</c>) to a culture-aware converted value.
    /// </summary>
    /// <param name="memberPath">The string deep-path to the target member.</param>
    /// <param name="value">The raw text value to convert and assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder With(string memberPath, string value);

    /// <summary>
    /// Sets the member at <paramref name="memberPath"/> to the result of building the model
    /// builder explicitly registered under <see cref="ModelBuilderAttribute"/> name
    /// <paramref name="builderName"/> for the member's type.
    /// </summary>
    IModelBuilder WithBuilder(LambdaExpression memberPath, string builderName);

    /// <summary>
    /// Sets multiple members at once from a sequence of deep-path/value pairs (e.g. the rows of a
    /// Gherkin table). Each value is applied as with <see cref="With(string, string)"/>.
    /// </summary>
    /// <param name="values">The member deep-path/value pairs to apply.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder WithValues(IEnumerable<KeyValuePair<string, string?>> values);

    /// <summary>
    /// Constructs a new model instance and applies all configured values to it.
    /// </summary>
    /// <returns>The built model.</returns>
    object Build();
}

/// <summary>
/// Strongly-typed fluent builder contract for <typeparamref name="TModel"/>. Configures member
/// values via type-safe lambda getters, nested builders or string deep-paths, and materializes the
/// model with <see cref="Build"/>.
/// </summary>
/// <typeparam name="TModel">The model type this builder builds.</typeparam>
public interface IModelBuilder<TModel>
{
    /// <summary>The model type this builder builds.</summary>
    Type ModelType { get; }

    /// <summary>
    /// Clears any configured values, returning the builder to its initial state.
    /// </summary>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> Reset();

    /// <summary>
    /// Builds onto an existing <paramref name="instance"/> instead of constructing a new one, and returns
    /// it. A one-shot terminal call like <see cref="Build"/> that does not change the builder's state, so
    /// Build still constructs a new instance before and after. Lets you compose a model over multiple
    /// datasets (e.g. several Gherkin tables) without cramming everything into one.
    /// </summary>
    TModel Extend(TModel instance);

    /// <summary>
    /// Sets the member selected by <paramref name="getter"/> to a fixed value.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="getter">A type-safe expression selecting the target member.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, TValue? value);

    /// <summary>
    /// Sets the member selected by <paramref name="getter"/> to a nested model configured through
    /// its own fluent builder.
    /// </summary>
    /// <typeparam name="TValue">The nested member's type.</typeparam>
    /// <param name="getter">A type-safe expression selecting the target member.</param>
    /// <param name="builder">A callback that configures the nested builder for the member.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder) where TValue: class;

    /// <summary>
    /// Sets the member selected by <paramref name="getter"/> to a value produced lazily at build time.
    /// </summary>
    /// <typeparam name="TValue">The member's type.</typeparam>
    /// <param name="getter">A type-safe expression selecting the target member.</param>
    /// <param name="valueFactory">A factory invoked at build time to produce the value.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<TValue?> valueFactory);

    /// <summary>
    /// Like <see cref="With{TValue}(Expression{Func{TModel,TValue}}, Func{TValue})"/>, but the
    /// factory receives the builder's own <see cref="IModelBuilderProvider"/> - guaranteed to be
    /// the correct one for this builder, even if the factory is a reusable function not written
    /// in a scope where the "right" provider is otherwise available (e.g. a shared faker-style
    /// helper used across multiple DI scopes/tests).
    /// </summary>
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilderProvider, TValue?> valueFactory);

    /// <summary>
    /// Sets the member at the given string deep-path (e.g. <c>"Address.Street"</c> or
    /// <c>"Lines[2].Amount"</c>) to a culture-aware converted value.
    /// </summary>
    /// <param name="memberPath">The string deep-path to the target member.</param>
    /// <param name="value">The raw text value to convert and assign.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> With(string memberPath, string value);

    /// <summary>
    /// Sets the member at <paramref name="getter"/> to the result of building the model builder
    /// explicitly registered under <see cref="ModelBuilderAttribute"/> name <paramref name="builderName"/>
    /// for <typeparamref name="TValue"/>. This is a separate method (rather than a `With` overload)
    /// because a generic `With&lt;TValue&gt;(getter, string)` overload would be ambiguous with
    /// `With&lt;TValue&gt;(getter, TValue value)` whenever TValue is itself `string`.
    /// </summary>
    IModelBuilder<TModel> WithBuilder<TValue>(Expression<Func<TModel, TValue>> getter, string builderName) where TValue : class;

    /// <summary>
    /// Sets multiple members at once from a sequence of deep-path/value pairs (e.g. the rows of a
    /// Gherkin table). Each value is applied as with <see cref="With(string, string)"/>.
    /// </summary>
    /// <param name="values">The member deep-path/value pairs to apply.</param>
    /// <returns>This builder, to allow call chaining.</returns>
    IModelBuilder<TModel> WithValues(IEnumerable<KeyValuePair<string, string?>> values);

    /// <summary>
    /// Constructs a new <typeparamref name="TModel"/> instance and applies all configured values to it.
    /// </summary>
    /// <returns>The built model.</returns>
    TModel Build();
}
