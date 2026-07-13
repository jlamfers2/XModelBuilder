namespace XModelBuilder;

/// <summary>
/// Tags a concrete <see cref="IModelBuilder"/> implementation with a MANDATORY, per-model-type
/// UNIQUE name, so it can be selected explicitly - via <see cref="IModelBuilderProvider.For(Type, string)"/>
/// or the "&lt;name&gt;" model-builder-reference syntax - when one or more specific builders are
/// registered for the same model type. Specific builders layer on top of the always-applied default
/// layer; there is no "default among them" to configure. Names are checked for uniqueness by
/// <c>ValidateXModelBuilderRegistrations()</c>.
/// </summary>
/// <param name="name">The mandatory, per-model-type unique builder name; must not be null or whitespace.</param>
/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModelBuilderAttribute(string name) : Attribute
{
    /// <summary>
    /// The builder's unique name within its model type, used to select it explicitly.
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Model builder name must not be null or whitespace.", nameof(name))
        : name;
}
