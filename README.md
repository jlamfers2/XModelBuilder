# XModelBuilder — User Guide & Technical Specification

This document describes (1) how to use XModelBuilder as a consumer of the library,
and (2) how the library works internally, down to the level of algorithms and
grammars. The goal of part (2) is that this document can also serve as a
specification for building a comparable framework from scratch, without having to
read the existing source code.

Target platform: .NET 8 (C#), nullable reference types enabled, implicit usings
enabled. Besides the core library (project XModelBuilder), the solution contains two
separate integration projects for Gherkin test frameworks: XModelBuilder.Reqnroll and
XModelBuilder.SpecFlow (see chapter 18).

## Table of contents

1.  [What is XModelBuilder?](#1-what-is-xmodelbuilder)
2.  [Installation and registration (Dependency Injection)](#2-installation-and-registration-dependency-injection)
3.  [Quick start](#3-quick-start)
4.  [Core concepts and public API](#4-core-concepts-and-public-api)
5.  [Multiple builders per model type: ModelBuilderAttribute and resolution order](#5-multiple-builders-per-model-type-modelbuilderattribute-and-resolution-order)
6.  [The "With" methods in detail](#6-the-with-methods-in-detail)
7.  [Deep paths: nested members and collections via string paths](#7-deep-paths-nested-members-and-collections-via-string-paths)
8.  [Constructor arguments: how XModelBuilder recognizes them](#8-constructor-arguments-how-xmodelbuilder-recognizes-them)
9.  [The mini data language for string values (arrays/objects as text)](#9-the-mini-data-language-for-string-values)
10. [ValueConverter: conversion rules, tokens, named builders and culture](#10-valueconverter-conversion-rules-tokens-named-builders-and-culture)
11. [Fakers: IFaker, registration, tokens and typed invocation](#11-fakers-ifaker-registration-tokens-and-typed-invocation)
12. [BuildMany: building multiple instances at once (and Extend: building onto an existing instance)](#12-buildmany-building-multiple-instances-at-once)
13. [Writing your own ModelBuilders](#13-writing-your-own-modelbuilders)
14. [Static use without a DI container (DefaultModelBuilderProvider)](#14-static-use-without-a-di-container)
15. [Build algorithm, instantiation fallbacks and edge cases](#15-build-algorithm-instantiation-fallbacks-and-edge-cases)
16. [Architecture / file overview](#16-architecture--file-overview)
17. [Full API reference (signatures)](#17-full-api-reference-signatures)
18. [Gherkin integration: Reqnroll and SpecFlow](#18-gherkin-integration-reqnroll-and-specflow)
19. [Known limitations](#19-known-limitations)
20. [Specification summary (for reimplementing this framework)](#20-specification-summary-for-reimplementing-this-framework)
21. [Deterministic generation with a seed (XFaker and BogusFaker)](#21-deterministic-generation-with-a-seed-xfaker-and-bogusfaker)

## 1. What is XModelBuilder?

XModelBuilder is a reflection-based framework for building and generating deterministic test data in .NET. It combines the Object Mother and Test Data Builder patterns with an orchestration layer, so that you can compose object graphs in a fluent way without having to write builders by hand for every model class.

The framework supports both manually configured and automatically generated test data, and can be integrated with faker libraries such as Bogus, whose integration ships by default. XModelBuilder acts as the central orchestrator that brings together builders, generated data and scenario-specific configuration into reproducible and maintainable test data sets.

XModelBuilder is designed for use in unit, integration and acceptance tests. Thanks to its integration with Reqnroll and SpecFlow it also fits BDD scenarios seamlessly.


Key features:

- A single generic base class (`ModelBuilder<TBuilder,TModel>`) that provides a
  builder for EVERY class, including classes with constructor parameters, read-only
  properties, init-only properties and private backing fields.
- Properties/fields can be set via:
  - Strongly-typed lambda expressions: `x => x.Name`
  - String paths with dot notation and array/list indexing: `"Address.Street"`,
    `"Lines[2].Quantity"`
  - One large set of key/value pairs (`WithValues`), e.g. coming from a
    configuration file, test data table or Gherkin table.
- Simple textual values (`"42"`, `"true"`, `"Monday"`) are converted automatically
  to the correct .NET type of the property (int, bool, enum, DateTime, Guid, ...),
  including culture-aware parsing.
- Textual values also support arrays (`"[1,2,3]"` or even just `"1,2,3"`),
  `HashSet<T>`, `Dictionary<TKey,TValue>` and nested object literals
  (`"{Street:\"Main Street\",Number:\"1\"}"`) to build complete nested models and
  collections from a single string (chapter 9).
- Special tokens `null()`, `new()` and `default()` (and their escaped form, see
  chapter 10) give fine-grained control over how a value is produced. In addition,
  for complex (model) types you can reference a SPECIFIC builder tagged with
  `[ModelBuilder("name")]` by using a bare name (see chapters 5 and 10).
- Custom "fakers" (methods on a class that implements `IFaker`) are callable as a
  `"name(args)"` token, WITH overloading and optional automatic Type/IServiceProvider
  injection for generic fixture methods. The same fakers are also callable in a TYPED
  way (`Faker<TFaker>()`, or simply directly via constructor injection) - see chapter 11.
- `BuildMany` builds multiple instances at once: on the builder (repeated `Build()`
  calls, shared base configuration) or on the provider (each a fresh builder,
  optionally per index or via a specific named builder) - see chapter 12.
- MULTIPLE builders can be registered for the same model type; each builder gets a
  mandatory, unique `[ModelBuilder("name")]` with which you request it explicitly, and
  you designate the default in an order-independent way with
  `UseAsDefaultModelBuilder<TBuilder>()` (chapter 5).
- Works both with `Microsoft.Extensions.DependencyInjection` and fully standalone
  (without a DI container) via a static provider.
- Separately installable integrations for Reqnroll and SpecFlow build models directly
  from Gherkin tables (chapter 18).

## 2. Installation and registration (Dependency Injection)

Add a project reference to XModelBuilder and register the services:

```csharp
using XModelBuilder.DependencyInjection;

services.AddXModelBuilder();
```

Optionally with culture configuration:

```csharp
services.AddXModelBuilder(options =>
{
    options.DefaultCulture  = CultureInfo.GetCultureInfo("nl-NL");
    options.DateTimeCulture = CultureInfo.GetCultureInfo("nl-NL");
});
```

`AddXModelBuilder` does three things:

1. Registers `ModelBuilderOptions` (via `Configure`, or with factory defaults if no
   configuration delegate is supplied). Default values:
   `DefaultCulture = CultureInfo.InvariantCulture`,
   `DateTimeCulture = CultureInfo.InvariantCulture`.
2. Registers a "keyed" fallback implementation for the open generic type
   `IModelBuilder<>` under the key `"default"`, implemented by
   `XModelBuilder.Default.DefaultModelBuilder<T>` (a builder that does nothing special
   in `SetDefaults()`). This means that for ANY model type T for which you have not
   registered anything specific, a working builder can still be resolved.
3. Registers `IModelBuilderProvider`
   (`XModelBuilder.DependencyInjection.ModelBuilderProvider`) - by default as a
   Singleton, or as Scoped when you pass `AddXModelBuilder(isolation:
   XModelBuilderIsolation.PerScope)` (see chapter 21.1 for when you want that - e.g. one
   scope per BDD scenario).

If you want to use a custom builder for a specific model type (for example to always
set certain defaults), register it additionally:

```csharp
services.AddModelBuilder<PersonBuilder>();
// or
services.AddModelBuilder(typeof(PersonBuilder));
```

You may do this MULTIPLE TIMES for the same model type: all registered builders remain
available (both via `For<TModel>()` and via explicit name resolution). See chapter 5
for how XModelBuilder determines which builder is "the" builder when more than one is
registered for the same model type.

To have all `IModelBuilder` implementations registered automatically (handy for larger
apps with many builders spread across assemblies):

```csharp
services.AddModelBuildersFromAssembly(typeof(SomeMarkerType).Assembly); // one assembly
services.AddModelBuildersFromAssemblies();                              // whole AppDomain (via AssemblyScanner)
```

This scans for all non-abstract, non-generic types that implement `IModelBuilder`, and
registers each via `AddModelBuilder(type)`. Because resolution is order-independent
(chapter 5), the scan order does not matter; with ≥2 builders per type, do not forget
to choose the default with `UseAsDefaultModelBuilder` and validate the whole set with
`ValidateXModelBuilderRegistrations()`.

For fakers there is DELIBERATELY no scanning - register them explicitly with
`AddFaker<T>()` (chapter 11).

## 3. Quick start

Example model:

```csharp
public class Address
{
    public string Street { get; set; }
    public string City   { get; set; }
}

public class Person
{
    public Person(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    private readonly string _name = null!;
    public string Name { get => _name; }
    public string City { get; init; } = null!;
    public string[] Options { get; }
    public Address Address { get; }
}
```

Building via lambda expressions (strongly typed):

```csharp
var person = xmodels.For<Person>()
    .With(x => x.Name, "John")
    .With(x => x.City, "Amsterdam")
    .With(x => x.Options, ["note"])
    .With(x => x.Address, b => b
        .With(a => a.Street, "Main Street")
        .With(a => a.City, "Amsterdam"))
    .Build();
```

You can also request a builder instance first and `Build()` it separately, and pass the
result as a value - functionally identical to the previous line:

```csharp
var address = xmodels.Use<ComplexAddressBuilder>().Build();
var person = xmodels.For<Person>()
    .With(x => x.Name, "John")
    .With(x => x.Address, address)
    .Build();
```

Building via string paths (for example from a test data table or CSV):

```csharp
var person = xmodels.For<Person>()
    .With("Name", "John")
    .With("City", "Amsterdam")
    .With("Options", "[note]")
    .With("Address", "{Street:\"Main Street\",City:\"Amsterdam\"}")
    .Build();
```

Or - if a builder named `"complex-address"` is registered for `Address` - by simply
providing that name as the value:

```csharp
var person = xmodels.For<Person>()
    .With("Name", "John")
    .With("Address", "complex-address")
    .Build();
```

Both examples produce a valid `Person` object, even though `Person` has no public
parameterless constructor, a read-only `Name` (only a private backing field), an
init-only `City` and a read-only `Address` that is filled by the constructor.

Without a DI container (static facade, see chapter 14):

```csharp
using XModelBuilder.Default;

var person = Create.Model<Person>(); // builds with all defaults
var custom = For.Model<Person>().With(x => x.Name, "Jane").Build();
```

## 4. Core concepts and public API

**`IModelBuilder<TModel>`**
The strongly-typed builder interface for a single model type. Methods:
`Reset`, `With` (4 overloads), `WithValues`, `Build`, `Extend` (build onto an existing
instance, chapter 12.1). See chapter 17.

**`IModelBuilder`**
The non-generic "shadow" interface with the same capabilities, but with
`LambdaExpression`/`object` instead of `Expression<Func<TModel,TValue>>`/`TValue`. This
allows code that does not know the model type at compile time (such as the provider,
which works based on a Type) to still work with a builder.

**`IModelBuilderProvider`**
Resolves builders. Methods: `For<TModel>()`, `For(Type)`, `For<TModel>(name)`,
`For(Type,name)`, `Use<TModelBuilder>()`, `Use(Type)`. `For` looks up a builder BASED ON
THE MODEL TYPE (with or without an explicit name, see chapter 5); `Use` returns a
specific, compile-time-known builder class directly, regardless of what is registered
for the model type.

**`ModelBuilder<TBuilder, TModel>`**
The abstract base class that implements `IModelBuilder<TModel>` and `IModelBuilder`. All
the logic lives here (constructor detection, deep paths, conversion). Custom builders
inherit from it and only implement the abstract method `SetDefaults()`.

**`ModelBuilderAttribute`**
Gives a concrete builder class a MANDATORY, per-model-type UNIQUE name
(`[ModelBuilder("name")]`), with which it can be requested explicitly (see chapter 5).
The name does NOT determine which builder is the default - that is configured in an
order-independent way with `UseAsDefaultModelBuilder<TBuilder>()` and validated with
`ValidateXModelBuilderRegistrations()`.

**`ModelBuilderOptions`**
- `DefaultCulture` (`CultureInfo`, default: `InvariantCulture`) - used for all
  conversions except DateTime/DateTimeOffset.
- `DateTimeCulture` (`CultureInfo`, default: `InvariantCulture`) - used specifically for
  DateTime/DateTimeOffset parsing.

## 5. Multiple builders per model type: ModelBuilderAttribute and resolution order

Normally you register at most one builder per model type. Sometimes, however, you want
multiple "variants" of a builder for the same model type available (for example a simple
and an extended variant), and yet designate an unambiguous "default". Resolution is
DELIBERATELY order-independent: it never depends on registration order, not even when
builders arrive from multiple assemblies via assembly scanning.

**`[ModelBuilder("name")]`**
An attribute on a concrete builder class (a class that derives from
`ModelBuilder<TBuilder,TModel>`). The name is MANDATORY and must be UNIQUE per model
type. The name does NOT determine the default (there is no longer a special name
`"default"`).

```csharp
[ModelBuilder("complex-address")]
public sealed class ComplexAddressBuilder(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xmodels)
    : ModelBuilder<ComplexAddressBuilder, Address>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(x => x.Street, "Main Street");
    }
}
```

Register as many builders for `Address` as you like, and - when there is more than one -
explicitly designate the default with `UseAsDefaultModelBuilder<TBuilder>()` (non-generic
variant: `UseAsDefaultModelBuilder(typeof(...))`). The model type is derived from the
builder, so no magic string:

```csharp
services
    .AddModelBuilder<ComplexAddressBuilder>()       // [ModelBuilder("complex-address")]
    .AddModelBuilder<SimpleAddressBuilder>()         // [ModelBuilder("simple")]
    .UseAsDefaultModelBuilder<SimpleAddressBuilder>(); // 'simple' is the default for Address
```

Resolution of `xmodels.For<TModel>()` / `xmodels.For(Type)`:

1. **0 builders** for that model type → the generic open-generic fallback
   (`DefaultModelBuilder<>`, or a fallback customized via `SetDefaultModelBuilder` - see
   chapter 14).
2. **1 builder** → that single one (a configured default is not required).
3. **≥2 builders** → the default configured with `UseAsDefaultModelBuilder`. If no
   default is configured, an `InvalidOperationException` is thrown (no silent choice, no
   "last one wins").

To EXPLICITLY use a specific named builder, regardless of the default:

```csharp
xmodels.For<Address>("complex-address")        // or
xmodels.For(typeof(Address), "complex-address")
```

This looks up the builder with EXACTLY that (unique) name, case-insensitively and fully
order-independently. If such a name does not exist, a `KeyNotFoundException` is thrown -
there is NO silent fallback to regular data conversion.

**Validation.** After all registrations, call `ValidateXModelBuilderRegistrations()` (on
the `IServiceCollection`, or `Validate()` on the standalone provider) to enforce the
rules all at once: every builder has a `[ModelBuilder]` name, names are unique per model
type, and every model type with ≥2 builders has a configured, actually registered
default. All violations are reported together in a single `InvalidOperationException`.

```csharp
services.ValidateXModelBuilderRegistrations(); // throws on duplicate name / missing default
```

This resolution works both via the DI provider
(`XModelBuilder.DependencyInjection.ModelBuilderProvider`, based on
Microsoft.Extensions.DependencyInjection's `GetServices(...)`) and via the static
`DefaultModelBuilderProvider`.

The same name can also be used AS A STRING VALUE in `With(string,string)` to build a
nested, complex property via a specific named builder - see chapter 10 ("named builder
reference").

## 6. The "With" methods in detail

`IModelBuilder<TModel>` offers the following ways to set a value:

**a) `With<TValue>(Expression<Func<TModel,TValue>> getter, TValue? value)`**
Sets a value directly. Works on shallow (`x => x.Name`) as well as deep paths
(`x => x.Address.Street`) and on array/list indexing (`x => x.Lines[0].Quantity`). The
value may also be the result of a separately requested builder, e.g.
`xmodels.Use<ComplexAddressBuilder>().Build()`.

**b) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<TValue?> valueFactory)`**
Like (a), but the value is computed lazily at the moment of `Build()` (not at the moment
of the `With` call).

**c) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder) where TValue : class`**
For nested models: gives you a builder for the type of the nested property, which you
configure further; the result of its `Build()` becomes the value. Internally this is
nothing more than variant (b) with `() => builder(xmodels.For<TValue>()).Build()` as the
value factory.

**d) `With(string memberPath, string? value)`**
Sets a value via a textual path (see chapter 7) and a textual value that is converted to
the correct type via `ValueConverter` (see chapter 10) - including the
`null()`/`new()`/`default()` tokens and named-builder references for complex types.

**e) `WithValues(IEnumerable<KeyValuePair<string,string?>> values)`**
Processes an entire set of paths/values at once (for example a row from a data table or
Gherkin table, see chapter 18). Each entry is evaluated separately: if the key exactly
matches (without a dot) a constructor parameter, it is used as a constructor argument;
otherwise it becomes a deep-path setting.

**f) `WithBuilder<TValue>(Expression<Func<TModel,TValue>> getter, string builderName) where TValue : class`**
The lambda equivalent of the named-builder-reference syntax (chapter 5/10): sets the
property to the result of building the builder registered under `[ModelBuilder(builderName)]`
for `TValue`, so functionally equal to
`With(getter, () => xmodels.For<TValue>(builderName).Build())`. This is DELIBERATELY its
own method, not a `With` overload: a generic `With<TValue>(getter, string)` overload
would be ambiguous with `With<TValue>(getter, TValue value)` as soon as `TValue` is
itself `string` (and that is precisely the most common `With` pattern, e.g.
`With(x => x.Name, "John")`).

**g) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<IModelBuilderProvider,TValue?> valueFactory)`**
Like (b), but the factory receives the builder's OWN `IModelBuilderProvider` (`_xmodels`)
as an argument, instead of you having to closure-capture it from an enclosing scope. This
is more than syntactic sugar: a REUSABLE factory function (e.g. a set of "fake value"
factories that you share independently of a specific test/provider) otherwise runs the
risk of capturing a STALE or WRONG provider in scenarios with scoped DI or parallel test
runs that each have their own `IServiceProvider`. With this form the factory ALWAYS gets
the correct provider for THIS builder:

```csharp
.With(x => x.Address, provider => provider.Faker<AddressFakers>().Random())
```

No overload ambiguity with form (b): `Func<TValue?>` and
`Func<IModelBuilderProvider,TValue?>` can always be distinguished by the number of lambda
parameters.

**`Reset()`**
Clears all previously supplied `With` settings and constructor arguments, and calls
`SetDefaults()` again. Handy for reusing the same builder instance for multiple,
slightly different models.

**`Build()`**
Creates the model (see chapter 15) and then applies all deep-path settings, in the order
in which they were supplied.

## 7. Deep paths: nested members and collections via string paths

A string path consists of one or more segments separated by dots. Each segment is a
member name, optionally followed by `"[index]"` to address an array or list element.

```
"Name"                      -> top-level member
"Address.Street"             -> nested member
"Lines[2].Quantity"          -> 3rd line (index 2), member "Quantity"
"Lines[2]"                   -> the 3rd line itself (no further member)
```

Rules for path resolution (apply to both the lambda variant and the string variant):

- Member resolution is case-insensitive and considers both public and non-public
  properties and fields.
- A property is only eligible if it has a setter (`CanWrite`). If it does not (e.g. an
  auto-property with only a getter), XModelBuilder looks for a backing field, in this
  order:
  1. A field with exactly the same name as the member (`"_name"` if the member is
     literally called `"_name"`, not `"Name"`).
  2. A field named `"_" + memberName` (e.g. `"_name"` for member `"Name"`).
  3. The compiler-generated backing field of an auto-property:
     `"<Name>k__BackingField"`.

  The first match that exists is used. If none of these exist, an
  `InvalidOperationException` is thrown (`"Unable to set ..."`).
- For a non-final segment without an index: if the current value of that member is null,
  a new instance is automatically built via the configured `IModelBuilderProvider` for
  the member type (so nested objects "auto-vivify" on demand) and assigned, before
  descending further.
- For the final segment without an index: the string value is converted to the declared
  type of the member via `ValueConverter` and assigned.
- For an indexed segment on an array: if the array is null or too short for the requested
  index, a new, larger array is created (existing elements are copied) and reassigned to
  the member.
- For an indexed segment on an `IList` (`List<T>`, etc.): if the member is null, a list
  is first built (via the provider, or - in the lambda variant - via `Activator` for
  interface/list types) and assigned; then the list is grown to at least `index+1`
  elements (added elements: `default(T)` for the final segment, otherwise instances built
  via the provider).
- When descending further after an indexed, non-final segment, the ACTUAL runtime type of
  the element (not the static collection element type) is used, so that polymorphic
  elements can be edited further correctly.

The lambda variant (`x => x.Lines[2].Quantity`) supports the same mechanisms, but reads
the structure from a C# expression tree instead of a string. Supported nodes:
`MemberExpression` (member access), `IndexExpression` and `ArrayIndex` (array/list
indexing), and `MethodCallExpression` to `get_Item` (as a fallback for indexer calls that
are not modeled as an `IndexExpression`). Only a SINGLE, CONSTANT, INTEGER index argument
is supported (no computed or variable indices, no multiple indexer arguments). Conversions
at the start of the expression (such as `x => (object) x.Name`) are automatically ignored.

## 8. Constructor arguments: how XModelBuilder recognizes them

Many model classes have properties that can only be set via the constructor (no setter,
no backing field, or deliberately immutable design). XModelBuilder supports this by
checking, on EVERY `With` call, whether the call represents a constructor argument BEFORE
writing it as a deep-path setting.

A `With` call is treated as a constructor argument if:

1. The model type does NOT use a "standard activator". That is: the chosen constructor
   (see chapter 15) has at least one mandatory (non-optional) parameter. If the
   constructor has no parameters, or they are all optional, the model is always created
   via a plain parameterless `Activator.CreateInstance` and there is therefore nothing to
   bind as a constructor argument.
2. The path is TOP-LEVEL and without a dot (so `"Address"`, not `"Address.Street"`), AND
   the name (case-insensitively) matches the name of one of the parameters of the chosen
   constructor.

If a `With` call satisfies this, the value (or value factory) is stored in an internal
table, coupled to the corresponding `ParameterInfo`. When building the model (see chapter
15), for each constructor parameter it first checks whether such a stored value exists; if
not, the parameter's own default value is used (or null).

Important: a path WITH a dot that happens to start with a constructor parameter name (e.g.
`"Address.Street"` while a parameter `address` exists) is NOT treated as a constructor
argument - it remains a deep-path setting that is applied only AFTER construction. This
means that if the member in question has no setter and no findable backing field, and no
separate `"Address"` setting is supplied to feed the constructor, building the model fails
with an exception (the constructor then receives null/default for that argument).

String values stored as constructor arguments are converted via `ValueConverter` at the
time of `Build()` (not earlier) to the parameter type, with the builder's
`DateTimeCulture`/`DefaultCulture` - so here too `null()`/`new()`/`default()` and
named-builder references work as usual (e.g. `.With("Address", "complex-address")` for a
ctor-only `Address` property).

## 9. The mini data language for string values

String values that represent arrays, lists or nested objects are parsed with a small,
custom language (comparable to, but not equal to, JSON). Below is the grammar in
EBNF-like notation:

```
value      := string | array | object | bareValue
string     := '"' { charOrEscape } '"'
charOrEscape := escape | <any character except '"' or '\'>
escape     := '\' ( '\' | '"' | 'n' | 'r' | 't' )
array      := '[' [ value { ',' value } ] ']'
object     := '{' [ pair { ',' pair } ] '}'
pair       := (string | bareValue) ':' value
bareValue  := { <any character not in reserved set> }
reserved   := '[' | ']' | '"' | '{' | '}' | ',' | ':' | ' ' | '\'
              | CR | LF | TAB
```

Additional rules:

- Whitespace (space, tab, CR, LF) between tokens is always insignificant and is skipped.
- Object keys are compared case-insensitively against the member names of the target type.
- A "bareValue" is simply all text up to the next reserved character; no escaping is
  needed for bare values (use strings for that).
- At the TOP LEVEL of an array conversion (i.e. when an entire string value is converted
  to an array or list type), the enclosing square brackets are OPTIONAL: both `"1,2,3"`
  and `"[1,2,3]"` are valid and produce the same result. Within nested structures (for
  example an array element that is itself an array) the brackets ARE required, because
  otherwise it is impossible to tell where one element stops and the next begins.
- Objects (`"{...}"`) must always be fully enclosed in braces at every level; there is no
  "bare object" variant.

Examples:

```
"42"                                  -> bareValue "42"
"[1,2,3]" or "1,2,3"                  -> array of three bareValues
"[[1,2],[3,4,5]]"                      -> array of two arrays
"{Street:\"Main Street\",Number:1}"   -> object with two fields
"{Address:{Street:\"Main Street\"}}"  -> nested objects
"[{Value:1},{Value:2}]"               -> array of object literals
```

The same grammar is also used for two .NET collection types that are not built up via
members, but converted AS A WHOLE target type (see chapter 10, steps 7-8):

- `Dictionary<TKey,TValue>` / `IDictionary<TKey,TValue>`: the "object" form (`"{...}"`) is
  interpreted as key/value pairs instead of member names of a POCO, e.g. `"{a:1,b:2}"` for
  `Dictionary<string,int>`.
- `HashSet<T>` / `ISet<T>`: the "array" form (brackets optional at the top level) is
  interpreted as the elements of the set, e.g. `"1,2,3"` or `"[1,2,3]"` for `HashSet<int>`.

Tuples (`Tuple<...>`/`ValueTuple<...>`) are currently NOT supported - see chapter 19.

## 10. ValueConverter: conversion rules, tokens, named builders and culture

Every string value that is assigned to a member or constructor parameter goes through the
following steps, in this order:

1. Trim the input.
2. **ESCAPING**: determine whether the trimmed input starts with exactly one `'@'`. If so,
   remove that single leading `'@'` character and skip steps 3 and 4 below - the rest of
   the text is from here on always treated as LITERAL data, never as a token/faker
   call/named-builder name. Example: `"@null()"` produces the literal text `"null()"` (for
   a string property), not the value null. To keep the `'@'` character itself AFTER
   escaping, use two leading @'s (`"@@new()"`) - after removing exactly one leading `'@'`,
   `"@new()"` remains, which - because we are already "isEscaped" - is simply treated as
   literal text.
3. (only if not escaped) Compare the input EXACTLY with the special tokens:
   - `"null()"` -> return null (for any target type).
   - `"new()"` -> return `Instantiator.CreateInstance(targetType)`: a "bare" instance,
     created with the most permissive constructor strategy (see chapter 15), WITHOUT using
     a registered ModelBuilder and without ever throwing an exception.
   - `"default()"` -> depends on the target type:
     - `Nullable<T>` -> null
     - `string` -> null
     - other value types -> `default(T)` (via `Activator`)
     - other reference types (classes) -> `provider.For(targetType).Build()`: builds via
       the builder that currently counts as "default" for that type (see chapter 5) - so
       including any `SetDefaults()` logic.
4. (only if not escaped, and no match in step 3) **FAKER CALL**: if the input matches the
   pattern `"name(args)"` (an identifier, immediately followed by parentheses, ending in
   `')'`), it is interpreted as a call to a registered `IFaker` method (see chapter 11):
   return `provider.InvokeFaker(name, args, targetType, culture)`. This applies to ANY
   target type (including value types and string), unlike the named-builder reference
   (step 12) which applies only to complex reference types.
5. If the target type is `string`, return the (possibly escaped) text directly.
6. If the (remaining) input is empty: return null for nullable/reference target types, or
   throw an `ArgumentException` for non-nullable value types.
7. If the target type is `Dictionary<TKey,TValue>` or `IDictionary<TKey,TValue>`: expect
   an object literal (`'{...}'`, see chapter 9); each key/value pair is converted key->TKey
   (via this same Convert function) and value->TValue (recursively via `ConvertObject`). If
   the input does not start with `'{'`, a `FormatException` is thrown.
8. If the target type is `HashSet<T>` or `ISet<T>`: parse the input with the array grammar
   (brackets optional at the top level, see chapter 9) and convert each element recursively
   to T; the result is a `HashSet<T>`.
9. If the target type is an array: parse the input with the array grammar described in
   chapter 9 and convert each element recursively to the element type.
10. If the target type is `List<T>`, `IList<T>`, `ICollection<T>` or `IEnumerable<T>`: same
    approach, the result is a `List<T>`.
11. If the (remaining) input starts with `'{'`: parse as an object literal (see chapter 9)
    and build an instance of the target type: an "empty" instance is first built via
    `provider.For(targetType).Build()`, after which, for each key/value in the object
    literal, the corresponding member (same resolution rules as chapter 7) is looked up and
    the value - recursively converted to the type of that member - is assigned.
12. **NAMED BUILDER REFERENCE**: if the input is NOT escaped, AND the target type is not a
    value type, not a string and not object (`typeof(object)`), then the (remaining) input
    is interpreted as the NAME of a `[ModelBuilder(name)]`-tagged builder for that target
    type (see chapter 5): return `provider.For(targetType, input).Build()`. If no builder
    with that name is registered for that type, a `KeyNotFoundException` is thrown - there
    is NO silent fallback to the steps below.
13. If the target type is an enum: parse by name (case-insensitively) or by numeric value.
    (This step - and the two below - are in practice only reached for value types, string
    or object, or for escaped input on a reference type, because step 12 otherwise already
    forces a builder-name lookup or throws an exception.)
14. If a known type converter is registered for the target type (see below), use it.
15. Otherwise: try `System.Convert.ChangeType` with the given culture.
16. If none of the above steps succeed, any exception is caught and rethrown as a
    `FormatException` with a clear message
    (`"Cannot convert X to target type Y. Missing converter for Y?"`).

Known, built-in type converters (all culture-aware, with support for thousands separators
on integers):

```
bool, byte, short, int, long, float, double, decimal,
DateTime, DateTimeOffset, TimeSpan, Guid, char
```

You can extend or override this set with:

```csharp
ValueConverter.AddKnownTypeConverter(typeof(MyType), (text, culture) => ...);
```

NOTE: this is a PROCESS-WIDE, static registration (not bound to a specific
`IModelBuilderProvider` or `ModelBuilderOptions` instance).

Culture choice: `DateTime` and `DateTimeOffset` always use
`ModelBuilderOptions.DateTimeCulture`; all other types use
`ModelBuilderOptions.DefaultCulture`. This allows you to combine, for example, Dutch date
formats (dd-MM-yyyy) with dot-decimal numbers, or vice versa.

Summary of the three tokens, faker calls and named-builder references, with their escaped
(literal) counterpart:

| Token | Meaning | Escaped (literal) |
|---|---|---|
| `null()` | explicit null | `@null()` |
| `new()` | bare instance (Instantiator, no builder) | `@new()` |
| `default()` | build via the current "default" builder (or CLR default for value types/string) | `@default()` |
| `name(args)` | call IFaker method "name" with args (any target type; see chapter 11) | `@name(args)` |
| `<name>` | build via the builder tagged `[ModelBuilder(<name>)]` for that type (only for non-string reference types) | `@<name>` |

## 11. Fakers: IFaker, registration, tokens and typed invocation

For test data that does not need to be fixed (ages, names, random text, ...) you write a
plain class with methods that implements `IFaker`. Those methods are callable in TWO ways:
dynamically via a `"name(args)"` token in the mini language
(`With(string,string)`/`WithValues`/Gherkin tables), or in a TYPED way directly in C# code.

**`IFaker`**
An empty marker interface. Every class that implements `IFaker` and is registered exposes
its (non-private, non-generic) instance methods as possible `"name(args)"` tokens (see
"Visibility rules" below), and can also be requested in its entirety in a typed way.

```csharp
public class PersonFakers : IFaker
{
    public DateTime AgeBetween(int minYears, int maxYears) =>
        DateTime.Today.AddYears(-Random.Shared.Next(minYears, maxYears + 1));

    public string RandomString() => "...";
    public string RandomString(int length) => "...";   // overload
}
```

Registration (DI):

```csharp
services.AddXModelBuilder()
    .AddFaker<PersonFakers>();                          // default: Singleton
    // or: .AddFaker<PersonFakers>(ServiceLifetime.Scoped) - e.g. to inject a
    //     per-scope seeded Random/Bogus Faker via the constructor of
    //     PersonFakers, for reproducible test data.
```

You DELIBERATELY always register fakers explicitly with `AddFaker<T>()` - there is no
assembly scanning for fakers. They are usually few (rarely more than a handful), so keeping
them explicit is clearer and avoids surprising order dependence. (Model builders ARE
numerous in larger apps; that is where scanning exists - see
`AddModelBuildersFromAssemblies()`, chapter 2/5.)

`AddFaker` registers the faker BOTH under its own concrete type and (forwarding to that
same instance/scope) under `IFaker` - the first form is for typed use (below), the second
for the dynamic token dispatch. This means you can also inject a faker as a PLAIN
DEPENDENCY, without ever going through XModelBuilder:

```csharp
public class PersonSteps(PersonFakers fakers)   // plain constructor injection
{
    public void Foo() => fakers.AgeBetween(1, 20);
}
```

Registration (standalone, without DI - see chapter 14):

```csharp
DefaultModelBuilderProvider.Current.AddFaker(new PersonFakers());        // ready-made instance
// or, to let the container construct it itself (with its own dependencies):
DefaultModelBuilderProvider.Current
    .AddServices(s => s.AddSingleton<SomeDependency>())
    .AddFaker<PersonFakers>();
```

Use via tokens (anywhere a string value is converted):

```csharp
.With("Birthday", "AgeBetween(1,20)")
.With("Name", "RandomString(5)")
.With("Name", "RandomString()")     // different overload, by argument count
```

Use TYPED, directly in C#:

```csharp
var age = xmodels.Faker<PersonFakers>().AgeBetween(1, 20);
// standalone/ambient variant (see chapter 14):
var age2 = Use.Faker<PersonFakers>().AgeBetween(1, 20);
```

`Faker<TFaker>()` (on `IModelBuilderProvider`) is the typed counterpart of the token
syntax: it returns the registered TFaker instance (the same instance as via plain
constructor injection, for Scoped/Singleton), with full IntelliSense/compile-time checking.
If no registration exists for TFaker, a `KeyNotFoundException` is thrown.

Resolution rules (token dispatch):

- Method matching is CASE-INSENSITIVE, like the rest of the library.
- If a method for name "X" is registered on MULTIPLE `IFaker` classes, the LAST REGISTERED
  class that has a method with that name wins COMPLETELY (consistent with the
  `[ModelBuilder]` resolution order from chapter 5) - overloads from different classes are
  not mixed.
- Within that class, an overload is considered "matching" when the number of supplied
  arguments lies between the number of MANDATORY (non-optional) and the TOTAL number of
  data parameters (see below for the Type/IServiceProvider parameter exception) - so
  OPTIONAL parameters may be omitted and are then filled with their default value - and
  when each supplied argument can be converted successfully to the parameter type (via the
  same ValueConverter conversion as everywhere else). Of the matching overloads, the one
  with the EXACT arity wins, otherwise the one with the fewest defaults to fill in. If no
  overload matches, a `MissingMethodException` is thrown; if the name does not exist at all, a

  `KeyNotFoundException`.
- The return type of the faker method (often `object`) is, after the call: passed through
  unchanged if it is already of the correct/compatible type; reparsed via this same Convert
  function if it is a string that still needs to be converted to another target type;
  otherwise passed through unchanged (any type mismatch then leads to a regular assignment
  error on the member).
- `"name(args)"` syntax is universally available (including for primitive target types),
  unlike the named-builder reference (bare name, no parentheses) which applies only to
  complex reference types - the presence/absence of parentheses always makes the two
  syntaxes distinguishable.
- Like the other tokens, `"name(args)"` can be escaped with a single leading `'@'` character
  if you want the literal text (e.g. because incidental data happens to look exactly like a
  faker call).

Automatic Type/IServiceProvider injection:

If the chosen method has one or more LEADING parameters of EXACTLY type `System.Type`
and/or `IServiceProvider` (matched purely by TYPE, not by parameter name; in any order
relative to each other), those parameters are NOT counted as a token argument and are
filled automatically: `Type` with the target type currently being converted to,
`IServiceProvider` with the IServiceProvider of the provider that made the call (for DI:
the container itself; for the standalone provider: its own internal, lazily built container
- see chapter 14). This lets you write a generic "give me a fake value of the correct type,
with access to other services" method:

```csharp
public object Fixture(Type type, IServiceProvider services) => ...
```

called as the token `"fixture()"` (zero arguments - `Type` and `IServiceProvider` do not
come from the token text, but from the context). The order of the two parameters does not
matter: `(Type, IServiceProvider)` and `(IServiceProvider, Type)` work identically.

Visibility rules (which methods count for token dispatch):

- PUBLIC, PROTECTED, INTERNAL and PROTECTED INTERNAL methods are all usable - both INSTANCE
  and STATIC methods. This allows you to deliberately make "framework-oriented" overloads
  (such as the Type/IServiceProvider variant above) PROTECTED: they are then callable via a
  token, but NOT via the typed `Faker<TFaker>()` route (where ordinary C# accessibility
  rules apply - a caller outside the class cannot call a protected member anyway, so this is
  enforced by the language itself, not by extra framework code). A STATIC method needs no
  instance state, but the class must still be registered as an (instance) faker
  (`AddFaker<T>()`/`AddFaker(instance)`) to be "found" via reflection - for typed use of a
  static method you do not need XModelBuilder at all, you just call it directly on the class
  (`MyFakers.SomeStaticMethod()`).
- PRIVATE methods (instance AND static) NEVER count for token dispatch.
- GENERIC methods (open generic method definitions, e.g. `T Create<T>()`) NEVER count for
  token dispatch - there is no token syntax to supply a type argument inline, and the
  Type-parameter auto-injection above already covers the "give me the correct type" scenario
  for regular, non-generic methods. Generic methods are therefore intended EXCLUSIVELY for
  typed invocation:

```csharp
xmodels.Faker<MyFakers>().Create<Address>()
```

Deep-path faker resolution (nested member paths):

A token name may ALSO be a dot-separated MEMBER PATH starting at a registered faker, instead
of a single method name. The first segment chooses the owner faker (the faker that has a
member with that name); the intermediate segments are read as a property/field or as a
parameterless method; and the final segment is called as a method - or, if no method with
that name exists AND no arguments are supplied, read as a property/field (the "terminal
property fallback"):

```csharp
.With("Name", "bogus.name.firstname()")    // Bogus.Faker -> Name dataset -> FirstName()
.With("City", "bogus.address.city()")
.With("Name", "bogus.person.firstname()")  // terminal is a property -> read via the fallback
```

This gives you the entire surface of an underlying object (such as a Bogus `Faker`)
available WITHOUT writing an adapter method for every generator: the faker only needs to
expose the object as a property (see chapter 21). The first-segment path immediately provides
a namespace, so such tokens do not collide with your other fakers. The last-registered-wins
and overload/optional-parameter rules above apply in full to the final segment. (A
combination such as `x.currency().code` - a method followed by a property as the last step -
is not expressible: the final segment is exactly one member; use the typed route for that.)

## 12. BuildMany: building multiple instances at once

Two `BuildMany`'s, in two different places, for two different scenarios: one on the BUILDER
(the same instance reused), one on the PROVIDER (each instance a fresh builder).

**a) On `IModelBuilder<TModel>` - the same, already configured builder:**

```csharp
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count);
```

Simply calls `Build()` `count` times on the SAME builder. Everything you had already set via
`With(...)` (lambda values, literals) is reused for EVERY instance; everything set via a
value factory or a string-path token (including faker calls) is RE-EVALUATED on EACH
`Build()` call - so that part CAN vary per instance:

```csharp
var people = xmodels.For<Person>()
    .With(p => p.City, "Amsterdam")           // shared by all 5
    .With("Name", "RandomFirstName()")         // 5 different names
    .BuildMany(5);
```

**b) On `IModelBuilderProvider` - each instance a fresh builder:**

```csharp
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count,
    Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName,
    Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
```

Each iteration gets a FRESH builder (from `provider.For<TModel>()`, or - with the
`modelBuilderName` forms - from `provider.For<TModel>(modelBuilderName)`, chapter 5), plus
optionally the (zero-based) index to configure per instance:

```csharp
var people = xmodels.BuildMany<Person>(5, (b, i) => b
    .With(p => p.Name, $"Person{i}")
    .With(p => p.Address, new Address()));

var dutchPeople = xmodels.BuildMany<Person>(5, "dutch-person", (b, i) => b
    .With(p => p.Name, $"Person{i}"));
```

When to use which form? Form (a) is the right choice as soon as you have ALREADY requested a
specific builder anyway (e.g. via `For<TModel>("name")` or `Use<TBuilder>()`) and have
already set configuration on it that you want to share across ALL instances - that cannot be
expressed with form (b), because each iteration there gets its OWN, empty builder (see chapter
18 for exactly the same trade-off with Gherkin's `CreateModel`/`CreateModels`). Form (b) is
the right choice as soon as you want a DIFFERENT, explicit builder name per instance, or
simply need no shared configuration at all.

On the static facade (chapter 14) form (b) is available as:

```csharp
Create.Models<TModel>(count)                              // == DefaultModelBuilderProvider.Current.BuildMany<TModel>(count)
Create.Models<TModel>(count, modelBuilderName)            // == ...BuildMany<TModel>(count, modelBuilderName)
Create.Models<TModel>(count, configure)                   // == ...BuildMany<TModel>(count, configure)
Create.Models<TModel>(count, modelBuilderName, configure) // == ...BuildMany<TModel>(count, modelBuilderName, configure)
```

### 12.1 Extend: building ONTO an existing instance

`Build()` always constructs a NEW instance. `Extend` does the same, but applies the
configured values to a GIVEN instance instead of creating a fresh one:

```csharp
TModel Extend(TModel instance);   // on IModelBuilder<TModel> (core)
```

This lets you build a model over MULTIPLE datasets (e.g. multiple Gherkin tables) without
cramming everything into one table: build the base, and augment it later.

```csharp
var order = xmodels.For<Order>().With(o => o.CustomerName, "Alice").Build(); // base

xmodels.For<Order>()
    .With(o => o.PaymentMethod, PaymentMethod.OnAccount)
    .Extend(order);   // applies this ONTO order and returns order
```

Properties (kept as intuitive as possible):

- **Same pipeline as `Build()`**: internally `CreateInstance()` returns the supplied instance
  instead of creating a new one, after which the `With`/`WithValues` values are applied on
  top. A `Build()` override (chapter 13) therefore ALSO runs, so that computed/derived fields
  are recomputed.
- **One-shot, terminal**: `Extend` does NOT change the internal builder state. You can call
  `Build()` both before and after `Extend`; each `Build()` creates a fresh instance again.
- **Everything you supply is applied**, regardless of setter/init/ctor/backing field. Because
  no constructor runs, values that would otherwise be constructor arguments are set directly
  on the existing instance (via the setter or the backing field). Members you do NOT supply
  keep their existing value.

For the Gherkin integration there is a handy variant on the provider that builds one nested
member from its own table and sets it on the existing instance - see chapter 18
(`xmodels.Extend(instance, x => x.Address, table)`).

## 13. Writing your own ModelBuilders

For most model types you do not need a custom builder: the generic `DefaultModelBuilder<T>`
(registered as the "default" fallback) works out of the box. Write a custom builder when you
want to establish standard defaults that are applied automatically every time, or when you
want to customize the build behavior.

```csharp
[ModelBuilder]   // optional; without the attribute the builder has no name
public sealed class PersonBuilder(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xmodels)
    : ModelBuilder<PersonBuilder, Person>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(x => x.Name, "Unknown");
        With(x => x.City, "Amsterdam");
    }
}
```

Register it (so it is used instead of the generic default for `Person`):

```csharp
services.AddModelBuilder<PersonBuilder>();
```

`SetDefaults()` is called from the constructor (via `Reset()`), so every new builder instance
- and every time you call `Reset()` - starts with these defaults. Later `With` calls simply
override them.

If you want to register multiple variants of a builder for the same model type and later
request them unambiguously by name, use `[ModelBuilder("name")]` - see chapter 5.

You can also override `CreateInstance()` or `ApplyDeepPathSetting()` for more advanced
behavior, but that is not needed for most scenarios.

**Computed (cross-field) defaults via a `Build()` override.** `Build()` is `virtual`:
override it, call `base.Build()` (all `With`/table values are then already applied) and
compute derived fields. Use the `protected` helper `SetMember(model, x => x.Field, value)` to
set the value - it uses the same member resolution as the deep paths (property setter,
init-only, or - if there is no setter - the backing field), so it also works on
read-only/init-only members:

```csharp
public override Product Build()
{
    var product = base.Build();
    if (product.PriceWithVat is null)                         // only if not supplied
        SetMember(product, x => x.PriceWithVat, product.Price * 1.21m);
    return product;
}
```

With a nullable/sentinel field you distinguish "not supplied" from an explicit value. The only
exception this does not cover is a value that is ITSELF a constructor argument AND depends on
another constructor argument; produce that before construction by overriding
`CreateInstance()` instead.

## 14. Static use without a DI container

For scripts, quick unit tests or contexts without a DI container,
`XModelBuilder.Default.DefaultModelBuilderProvider.Current` offers a process-wide singleton
provider that implements the same `IModelBuilderProvider` contract.

**Important architectural detail:** this is NOT a separate, hand-written resolution
algorithm. Internally, `DefaultModelBuilderProvider` maintains its own `ServiceCollection`,
and - lazily, on the next call after a change - a REAL `IServiceProvider` is built and wrapped
in the same `XModelBuilder.DependencyInjection.ModelBuilderProvider` that the DI integration
also uses. Each `Add*`/`Set*` call below marks the internal `ServiceCollection` "dirty"; the
next `For`/`Use`/`Faker` call then rebuilds the `IServiceProvider`. This means: you can keep
registering at any time, even AFTER earlier use, exactly as before - but without a second,
hand-maintained resolution implementation existing alongside the DI version. It is also the
reason why `IServiceProvider` auto-injection in fakers (chapter 11) simply works here too:
there IS now always a real `IServiceProvider`, even without you having set one up yourself.

```csharp
DefaultModelBuilderProvider.Current
    .SetDefaultModelBuilder<MyOpenGenericBuilder>()   // change the open-generic fallback
                                                       // (instead of DefaultModelBuilder<>)
    .AddModelBuilder<PersonBuilder>()                  // register a specific builder for
                                                       // Person (may be multiple per model type)
    .AddFaker(new PersonFakers())                      // ready-made instance
    .AddFaker<OtherFakers>()                           // or: the container constructs it
    .AddServices(s => s.AddSingleton<SomeDependency>()) // escape hatch: register something
                                                       // arbitrary (e.g. a dependency that a
                                                       // container-built faker needs)
    .AddOptions(o => o.DefaultCulture = ...);          // reconfigure culture
```

Four static convenience classes sit thinly on top of this:

```csharp
For.Model<T>()           // == DefaultModelBuilderProvider.Current.For<T>()
Use.Builder<TBuilder>()  // == DefaultModelBuilderProvider.Current.Use<TBuilder>()
Use.Faker<TFaker>()      // == DefaultModelBuilderProvider.Current.Faker<TFaker>()
Create.Model<T>()        // == DefaultModelBuilderProvider.Current.For<T>().Build()
Create.Models<T>(...)    // == DefaultModelBuilderProvider.Current.BuildMany<T>(...) (chapter 12)
```

`Use` differs from `For`: `For<T>()` looks up a builder based on the MODEL TYPE T (per the
order-independent rule from chapter 5: one builder, otherwise the default configured with
`UseAsDefaultModelBuilder`, otherwise the generic fallback); `Use<TBuilder>()` instantiates a
SPECIFIC, compile-time-known builder class directly, regardless of whether anything is
registered for the corresponding model type. This is handy if you have multiple,
differently-named builders for one model type (for example to model different "scenarios") and
you know exactly which one you want in code. `Use.Faker<TFaker>()` is the same idea, but for
fakers (chapter 11).

## 15. Build algorithm, instantiation fallbacks and edge cases

**Constructor selection** (once per closed `TModel` type, statically cached):

1. Request `typeof(TModel).GetConstructors()` (ONLY PUBLIC constructors).
2. If there are zero public constructors: there is no "model constructor"; building falls back
   later to the Instantiator fallback (see below). NO exception is thrown at this point.
3. If there is exactly one public constructor: use it.
4. If there are several: choose the constructor with the fewest parameters (in case of a tie:
   the first that `GetConstructors()` returns - no further tie-breaking).
5. "Standard activator" flag: true if the chosen constructor has zero parameters, OR if ALL
   parameters are optional (i.e. a plain `Activator.CreateInstance(typeof(TModel))` suffices).

**`CreateInstance()`** (called at the start of every `Build()`):

a. If the "standard activator" flag is set: `Activator.CreateInstance(typeof(TModel))`.
b. Otherwise, if no model constructor was found (step 2 above):
   `Instantiator.CreateInstance(typeof(TModel))` - see below.
c. Otherwise: build the argument list for the chosen constructor. For each parameter: is there
   a stored constructor argument (chapter 8)? Use the value factory (if present), otherwise the
   stored value (a string is here still converted to the parameter type via `ValueConverter`),
   otherwise - if nothing is stored - the parameter's own default value, otherwise null. Call
   the constructor with these arguments.

**`Instantiator.CreateInstance(Type)`** - the "always create me an instance, no matter what"
fallback, used by both `CreateInstance()` (step b) and the `"new()"` token in ValueConverter:

1. Look (reflection, public + non-public, instance) for a PARAMETERLESS constructor. If it
   exists, call it directly (this also works for PRIVATE/PROTECTED constructors).
2. If it does not exist: choose the constructor (public + non-public) with the fewest
   parameters.
3. Build an argument list: string parameters get `""`, value-type parameters get their default
   value (via `Activator.CreateInstance` on the parameter type), reference-type parameters get null.

4. Call that constructor with the synthesized arguments.
5. If that call throws an exception (for example due to validation logic in the constructor
   body), fall back to `RuntimeHelpers.GetUninitializedObject(modelType)`: a CLR primitive that
   allocates an object WITHOUT running any constructor. This guarantees that an instance is
   ALWAYS returned, never an exception.

**`Build()` (full order):**

1. `CreateInstance()` (as above).
2. For each deep-path setting previously supplied via `With`/`WithValues`, in the order
   supplied: apply it to the freshly created instance (chapter 7).
3. Return the instance.

**`Reset()`:**
Clears the internal list of deep-path settings and the table of constructor arguments, and
calls `SetDefaults()` again.

## 16. Architecture / file overview

**Public API** (namespace `XModelBuilder` / `XModelBuilder.Default` /
`XModelBuilder.DependencyInjection`):

| File | Type(s) | Description |
|---|---|---|
| `IModelBuilder.cs` | `IModelBuilder`, `IModelBuilder<TModel>` | builder contracts |
| `IModelBuilderProvider.cs` | `IModelBuilderProvider` | resolution contract |
| `ModelBuilder.cs` | `ModelBuilder<TBuilder,TModel>` | core implementation |
| `ModelBuilderOptions.cs` | `ModelBuilderOptions` | culture settings |
| `ModelBuilderAttribute.cs` | `ModelBuilderAttribute` | mandatory, unique name tag for builders (chapter 5) |
| `IFaker.cs` | `IFaker` | marker interface for faker classes (chapter 11) |
| `ModelBuilderProviderExtensions.cs` | `BuildMany<TModel>(...)` on `IModelBuilderProvider` | extension methods (chapter 12) |
| `ModelBuilderExtensions.cs` | `BuildMany<TModel>(...)` on `IModelBuilder<TModel>` | extension method (chapter 12) |
| `Default/DefaultModelBuilder.cs` | `DefaultModelBuilder<TModel>` | "no defaults" builder |
| `Default/DefaultModelBuilderProvider.cs` | `DefaultModelBuilderProvider` | thin, lazy-ServiceProvider-based static singleton provider (no resolution logic of its own, see chapter 14) |
| `Default/For.cs`, `Default/Use.cs`, `Default/Create.cs` | `For`, `Use`, `Create` | static convenience facades |
| `DependencyInjection/ModelBuilderProvider.cs` | `ModelBuilderProvider` | DI-based provider; the ONLY place with real resolution logic (also used by the standalone provider above) |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddXModelBuilder`, `AddModelBuilder`, `AddModelBuildersFromAssembly`, `AddModelBuildersFromAssemblies`, `AddDefaultModelBuilder`, `UseAsDefaultModelBuilder`, `ValidateXModelBuilderRegistrations`, `AddFaker` | registration extensions |
| `DependencyInjection/ModelBuilderDefaults.cs` | `ModelBuilderDefaults` (internal) | order-independent registry (model type → default builder), populated by `UseAsDefaultModelBuilder`, consulted by the provider (chapter 5) |
| `DependencyInjection/XModelBuilderIsolation.cs` | `XModelBuilderIsolation` (enum), `XModelBuilderIsolationState` (internal) | isolation choice (Shared/PerScope) + order-independent "last one reconciles" wiring of provider/fakers/seeders (chapter 21.1) |
| `DependencyInjection/AssemblyScanner.cs` | `AssemblyScanner` (internal) | scans the AppDomain (loading + caching the bin folder, cache invalidation on `AssemblyLoad`) for `AddModelBuildersFromAssemblies`; degrades gracefully on non-loadable dependencies (`ReflectionTypeLoadException`/`IsVisible`) |

**Internal helper logic** (namespace `XModelBuilder.Core`, all `internal`, except
`FriendlyNameExtensions` which is public for error messages):

| File | Description |
|---|---|
| `Core/Parser.cs` | static façade around `DataParser` |
| `Core/DataParser.cs` | parser for the mini data language (chapter 9) |
| `Core/CharScanner.cs` | character-by-character scanner with contextual error messages, used by `DataParser` |
| `Core/ValueConverter.cs` | all conversion logic (chapter 10), including Dictionary/HashSet conversion and faker token recognition (regex on `"name(args)"`) |
| `Core/IFakerInvocationSource.cs` | internal-only interface with `InvokeFaker(...)`; DELIBERATELY NOT a member of the public `IModelBuilderProvider` (chapter 11) - only the two built-in providers implement it, `ValueConverter` does a type check (`provider is IFakerInvocationSource`) and throws a `NotSupportedException` if a custom `IModelBuilderProvider` implementation lacks it and a faker token is used anyway |
| `Core/FakerInvoker.cs` | overload resolution and invocation of `IFaker` methods (chapter 11): visibility and generic filtering, Type/IServiceProvider auto-injection. Shared by the DI provider and `DefaultModelBuilderProvider`, which each supply only their own list of registered `IFaker` instances and their own `IServiceProvider` |
| `Core/StringPathSetter.cs` | applies string deep paths to an object (chapter 7) |
| `Core/LambdaPathSetter.cs` | applies lambda-expression deep paths to an object (chapter 7) |
| `Core/Instantiator.cs` | "always an instance" fallback (chapter 15) |
| `Core/HelperExtensions.cs` | reflection helpers: member resolution (`TryGetWritableMember`), member get/set, list element type detection, list growing, lambda path parsing, `ModelBuilderAttribute` name resolution (`GetModelBuilderName`, `HasModelBuilderName`, `GetModelType`), Dictionary/HashSet type-argument detection (`GetDictionaryTypeArgumentsOrNull`, `GetSetElementTypeOrNull`) |
| `Core/FriendlyNameExtensions.cs` | readable type names for error messages (e.g. `"List<Person>"` instead of `"List\`1"`) |

NuGet dependencies of the core project: `Microsoft.Extensions.DependencyInjection` (the FULL
package, not just `.Abstractions` - needed because `DefaultModelBuilderProvider` itself builds a
`ServiceCollection`/`ServiceProvider`, not merely consuming the interfaces) and
`Microsoft.Extensions.Options.ConfigurationExtensions`.

Separate integration projects (see chapter 18):

| File | Description |
|---|---|
| `XModelBuilder.Reqnroll/ReqnrollTableExtensions.cs` | Extension methods `CreateModel<T>`/`CreateModels<T>` on `Reqnroll.Table` |
| `XModelBuilder.SpecFlow/SpecFlowTableExtensions.cs` | The same extension methods on `TechTalk.SpecFlow.Table` |
| `XModelBuilder.Fakers.XFaker/Faker.cs` (+ `ServiceCollectionExtensions.cs`, `ModelBuilderProviderExtensions.cs`) | Dependency-free `Faker` with deterministic primitives, `AddXFaker(seed)` and the convenience accessor `provider.XFaker()` (chapter 21) |
| `XModelBuilder.Fakers.Bogus/BogusFaker.cs` (+ `ServiceCollectionExtensions.cs`, `ModelBuilderProviderExtensions.cs`) | `BogusFaker` (exposes a seeded Bogus `Faker`), `AddBogusFaker(seed)` and the convenience accessor `provider.Bogus()` (chapter 21) |

## 17. Full API reference (signatures)

```csharp
public interface IModelBuilder
{
    Type ModelType { get; }
    IModelBuilder Reset();
    IModelBuilder With(LambdaExpression memberPath, object? value);
    IModelBuilder With(LambdaExpression memberPath, Func<object?> valueFactory);
    IModelBuilder With(LambdaExpression memberPath, Func<IModelBuilderProvider, object?> valueFactory);
    IModelBuilder With(string memberPath, string value);
    IModelBuilder WithBuilder(LambdaExpression memberPath, string builderName);
    IModelBuilder WithValues(IEnumerable<KeyValuePair<string, string?>> values);
    object Build();
    object Extend(object instance);   // builds onto an existing instance (chapter 12.1)
}

public interface IModelBuilder<TModel>
{
    Type ModelType { get; }
    IModelBuilder<TModel> Reset();
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, TValue? value);
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter,
        Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder) where TValue : class;
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<TValue?> valueFactory);
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilderProvider, TValue?> valueFactory);
    IModelBuilder<TModel> With(string memberPath, string value);
    IModelBuilder<TModel> WithBuilder<TValue>(Expression<Func<TModel, TValue>> getter, string builderName) where TValue : class;
    IModelBuilder<TModel> WithValues(IEnumerable<KeyValuePair<string, string?>> values);
    TModel Build();
    TModel Extend(TModel instance);   // builds onto an existing instance (chapter 12.1)
}

public interface IModelBuilderProvider
{
    IModelBuilder For(Type modelType);
    IModelBuilder<TModel> For<TModel>() where TModel : class;
    IModelBuilder For(Type modelType, string name);
    IModelBuilder<TModel> For<TModel>(string name) where TModel : class;
    TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder;
    IModelBuilder Use(Type modelBuilderType);
    // Fresh, built-in DefaultModelBuilder<TModel> - bypassing any (custom/fallback) registration.
    IModelBuilder<TModel> NewDefaultModelBuilder<TModel>() where TModel : class;
    TFaker Faker<TFaker>() where TFaker : IFaker;
    // NO InvokeFaker here - that is internal-only plumbing, see
    // Core/IFakerInvocationSource.cs (chapter 16).
}

// Marker interface: every non-private, non-generic instance method of a
// registered implementation is callable as a "name(args)" token (chapter 11).
public interface IFaker { }

public static class ModelBuilderProviderExtensions
{
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
}

public static class ModelBuilderExtensions
{
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count);
}

public abstract class ModelBuilder<TBuilder, TModel> : IModelBuilder<TModel>, IModelBuilder
    where TModel : class
    where TBuilder : ModelBuilder<TBuilder, TModel>
{
    protected ModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels);
    protected abstract void SetDefaults();
    public virtual TModel Build();
    public TModel Extend(TModel instance);   // chapter 12.1
    protected virtual TModel CreateInstance();
    protected virtual void ApplyDeepPathSetting(TModel model, DeepPathSetting setting);
    // Sets a member (property setter / init-only / backing field) on an already-built model;
    // handy in a Build() override for computed defaults (chapter 13).
    protected void SetMember<TValue>(TModel model, Expression<Func<TModel, TValue>> member, TValue? value);
    // + all members of IModelBuilder<TModel> and IModelBuilder, strongly
    //   typed, returning as TBuilder where possible.
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModelBuilderAttribute(string name) : Attribute   // name is mandatory + unique per model type
{
    public string Name { get; }
}

public class ModelBuilderOptions
{
    public CultureInfo DefaultCulture { get; set; }  // default: InvariantCulture
    public CultureInfo DateTimeCulture { get; set; } // default: InvariantCulture
}

public enum XModelBuilderIsolation { Shared, PerScope }   // chapter 21.1

public static class ServiceCollectionExtensions
{
    IServiceCollection AddXModelBuilder(this IServiceCollection services,
        Action<ModelBuilderOptions>? configure = null,
        XModelBuilderIsolation isolation = XModelBuilderIsolation.Shared);
    // Registers seeder services whose lifetime follows the isolation (order-independent);
    // used by AddXFaker/AddBogusFaker.
    IServiceCollection AddIsolatedXModelBuilderServices(this IServiceCollection services,
        Action<IServiceCollection, ServiceLifetime> register);
    IServiceCollection AddModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection AddModelBuilder<TModelBuilder>(this IServiceCollection services)
        where TModelBuilder : IModelBuilder;
    IServiceCollection AddModelBuildersFromAssembly(this IServiceCollection services, Assembly assembly);
    IServiceCollection AddModelBuildersFromAssemblies(this IServiceCollection services);   // whole AppDomain
    IServiceCollection AddDefaultModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection UseAsDefaultModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection UseAsDefaultModelBuilder<TModelBuilder>(this IServiceCollection services)
        where TModelBuilder : IModelBuilder;
    IServiceCollection ValidateXModelBuilderRegistrations(this IServiceCollection services);
    IServiceCollection AddFaker(this IServiceCollection services, Type fakerType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton);
    IServiceCollection AddFaker<TFaker>(this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker;
}

public sealed class DefaultModelBuilderProvider : IModelBuilderProvider
{
    public static DefaultModelBuilderProvider Current { get; }
    public DefaultModelBuilderProvider SetDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider SetDefaultModelBuilder(Type defaultModelBuilderType);
    public DefaultModelBuilderProvider AddModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider AddModelBuilder(Type modelBuilderType);
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder(Type modelBuilderType);
    public DefaultModelBuilderProvider Validate();   // == ValidateXModelBuilderRegistrations
    public DefaultModelBuilderProvider AddFaker(IFaker faker);
    public DefaultModelBuilderProvider AddFaker<TFaker>(ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker;
    public DefaultModelBuilderProvider AddServices(Action<IServiceCollection> configure);
    public DefaultModelBuilderProvider AddOptions(Action<ModelBuilderOptions>? configure = null);
    // + For/For<T>/For(Type,string)/For<T>(string)/Use/Use<T>/Faker<T> from IModelBuilderProvider
}

public static class For   { public static IModelBuilder<TModel> Model<TModel>() where TModel : class; }
public static class Use
{
    public static TModelBuilder Builder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public static TFaker Faker<TFaker>() where TFaker : IFaker;
}
public static class Create
{
    public static TModel Model<TModel>() where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
}

// XModelBuilder.Reqnroll / XModelBuilder.SpecFlow (see chapter 18):
public static class ReqnrollTableExtensions   // resp. SpecFlowTableExtensions
{
    // Configurable, language-dependent vertical-table column names (default: EN + NL):
    public static IReadOnlyList<VerticalTableHeader> VerticalTableHeaders { get; }        // read-only
    public static void Configure(Action<ReqnrollTableOptions> configure);                 // resp. SpecFlowTableOptions

    TModel CreateModel<TModel>(this IModelBuilder<TModel> builder, Table table);
    IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table) where TModel : class;
    IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table, string modelBuilderName) where TModel : class;

    IModelBuilder<TModel> WithValue<TModel, TValue>(this IModelBuilder<TModel> builder,
        Expression<Func<TModel, TValue>> member, Table table) where TValue : class;
    TModel Extend<TModel, TValue>(this IModelBuilderProvider provider,
        TModel instance, Expression<Func<TModel, TValue>> member, Table table)
        where TModel : class where TValue : class;
}

public readonly record struct VerticalTableHeader(string FieldColumn, string ValueColumn);

public sealed class ReqnrollTableOptions   // resp. SpecFlowTableOptions
{
    public IList<VerticalTableHeader> VerticalTableHeaders { get; set; }   // seeded with the current conventions
}
```

## 18. Gherkin integration: Reqnroll and SpecFlow

For projects that use Gherkin/BDD steps, there are two separate class-library projects (each
with their own NuGet dependency, so that a project using only Reqnroll does not also have to
install SpecFlow, and vice versa):

- `XModelBuilder.Reqnroll` → extension methods on `Reqnroll.Table`
- `XModelBuilder.SpecFlow` → extension methods on `TechTalk.SpecFlow.Table`

Both offer EXACTLY the same extension methods (in their own namespace, `XModelBuilder.Reqnroll`
resp. `XModelBuilder.SpecFlow`) - deliberately spread across TWO different "anchor types", not
all on `Table`:

```csharp
TModel CreateModel<TModel>(this IModelBuilder<TModel> builder, Table table);

IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table)
    where TModel : class;

IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table, string modelBuilderName)
    where TModel : class;

// Build one NESTED member from its OWN table (instead of everything in one table):
IModelBuilder<TModel> WithValue<TModel, TValue>(this IModelBuilder<TModel> builder,
    Expression<Func<TModel, TValue>> member, Table table) where TValue : class;

// Same, but onto an EXISTING instance (multi-table across multiple steps):
TModel Extend<TModel, TValue>(this IModelBuilderProvider provider,
    TModel instance, Expression<Func<TModel, TValue>> member, Table table)
    where TModel : class where TValue : class;
```

**`WithValue(member, table)`** sets one member to the value of a `TValue` that is built from
`table` (via `TValue`'s own builder), and continues in the fluent chain. This is how you fill a
nested member from its OWN table:

```csharp
var customer = xmodels.For<Customer>()
    .With(c => c.Name, "Alice")
    .WithValue(c => c.Address, addressTable)   // Address from a separate table
    .Build();
```

**`Extend(instance, member, table)`** (on the provider) does the same but on an ALREADY BUILT
instance, and returns it - handy for composing a model over multiple Gherkin steps/tables:

```csharp
xmodels.Extend(customer, c => c.Address, addressTable);   // sets only customer.Address
```

Important: this `Extend` applies the set via a FRESHLY constructed, built-in
`DefaultModelBuilder<TModel>` (`provider.NewDefaultModelBuilder<TModel>()`) - NOT via `TModel`'s
own (custom) builder. As a result, that builder's `SetDefaults`/`Build()` override does NOT run:
it is guaranteed that ONLY that one member is set, without other fields being accidentally
(re)populated. The nested `TValue` (e.g. `Address`) IS built with its own builder.

`CreateModel` (singular) hangs off an ALREADY REQUESTED builder (via `xmodels.For<TModel>()` or
`xmodels.Use<TBuilder>()`, chapter 4) instead of off the table or the provider. There are two
reasons for this:

- Consistency: all other "build a model" calls in XModelBuilder already hang off the
  builder/provider (`For`, `Use`, `With`, `Build`, `BuildMany`) - not off an arbitrary data
  source. One mental model: you first request a builder, and "feed" it with data, whether via
  `With()`, `WithValues()` or now `CreateModel(table)`.
- It lets you configure BEFOREHAND by hand and have the table applied on top, because it is the
  same builder instance that calls `WithValues(...)` and `Build()`:

```csharp
var person = xmodels.For<Person>()
    .With(p => p.Country, "NL")    // fixed value, not from the table
    .CreateModel(table);            // table values override/fill the rest
```

`CreateModels` (plural) DOES hang off the provider, not off a builder instance. This is not an
inconsistency but a consequence of a different number of required builder instances: a
horizontal table with N rows describes N INDEPENDENT instances, which each need their OWN
`Build()`. If you implemented this on a single builder instance by calling `Reset()` between
rows, any manual pre-configuration (such as the Country example above) would be lost after the
FIRST row, because `Reset()` also clears that pre-configuration, not just the table values of
the previous row. By keeping `CreateModels` on the provider (like `BuildMany`, chapter 12),
each row simply gets its own fresh builder via `For<TModel>()`, without that pitfall.

Both methods AUTOMATICALLY (intelligently) recognize which of the two common Gherkin table
shapes was used, and convert it into one or more calls to `WithValues(...)` - so all the normal
conversion rules (chapters 9 and 10, including `null()`/`new()`/`default()`, faker calls and
named builder references) simply apply to the cell values.

**Shape 1 - VERTICAL ("Field/Value"):** the table has EXACTLY two columns, and the column
headers (case-insensitive, trimmed) match one of the configured conventions. Each row then
describes ONE member of ONE instance:

```gherkin
| Field | Value     |
| Name  | John      |
| City  | Amsterdam |
```

**Column names are CONFIGURABLE (language-dependent).** The conventions are not hardcoded, but
live in the integration package (i.e. in the Reqnroll/SpecFlow layer, not in the core - the core
knows nothing about tables). You read them via a read-only property and change them via
`Configure`:

```csharp
// read-only view of the current conventions:
public static IReadOnlyList<VerticalTableHeader> ReqnrollTableExtensions.VerticalTableHeaders { get; }
public static IReadOnlyList<VerticalTableHeader> SpecFlowTableExtensions.VerticalTableHeaders { get; }

// changing them (process-wide; the package registers no services, so no DI Add):
public static void ReqnrollTableExtensions.Configure(Action<ReqnrollTableOptions> configure);
public static void SpecFlowTableExtensions.Configure(Action<SpecFlowTableOptions> configure);

public readonly record struct VerticalTableHeader(string FieldColumn, string ValueColumn);
```

By default they contain both English and Dutch: (`"field"`,`"value"`), (`"key"`,`"value"`),
(`"name"`,`"value"`), (`"property"`,`"value"`), (`"veld"`,`"waarde"`), (`"eigenschap"`,`"waarde"`),
(`"sleutel"`,`"waarde"`). Typically call `Configure` once at test-run start; the options are
seeded with the current conventions, so you can add to or replace the list entirely:

```csharp
// add a language:
ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders.Add(new("champ", "valeur")));

// or replace entirely:
ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders =
[
    new("champ", "valeur"),
]);
```

This is how `| Veld | Waarde |` works out of the box in a Dutch-language feature file:

```gherkin
| Veld | Waarde    |
| Name | John      |
| City | Amsterdam |
```

**Shape 2 - HORIZONTAL:** every other table shape (including a table that happens to have two
columns which do NOT satisfy the naming convention above, such as an entity with exactly two
properties). The column headers are then the member names, and each data row describes ONE
instance:

```gherkin
| Name | City      |
| John | Amsterdam |
| Jane | Utrecht   |
```

**`CreateModel(builder, table)`:**

- Vertical table: always builds exactly one instance (all rows combined).
- Horizontal table with EXACTLY one data row: builds that single instance.
- Horizontal table with 0 or more than 1 data row: throws an `InvalidOperationException` ("use
  `CreateModels<T>()` on the provider for a list").

**`CreateModels<TModel>(provider, table)`:**

- Vertical table: returns a list with EXACTLY ONE element (a vertical table can by definition
  describe only one instance).
- Horizontal table: returns one instance per data row, in table order, each via its own fresh
  builder (`provider.For<TModel>()` - so per the resolution of chapter 5: the single builder,
  otherwise the default configured with `UseAsDefaultModelBuilder`, otherwise the generic
  fallback).

**`CreateModels<TModel>(provider, table, modelBuilderName)`:**

Same as above, but each row EXPLICITLY uses the builder registered under
`[ModelBuilder(modelBuilderName)]` for `TModel` (via `provider.For<TModel>(modelBuilderName)`,
chapter 5) - regardless of which builder would normally count as the "default". If that name
does not exist, the VERY FIRST row throws a `KeyNotFoundException`.

Example (Reqnroll step):

```csharp
using XModelBuilder.Reqnroll;

[Given("the following person")]
public void GivenTheFollowingPerson(Table table)
{
    var person = _xmodels.For<Person>().CreateModel(table);
    // or, for a specifically registered builder:
    var person2 = _xmodels.Use<PersonBuilder>().CreateModel(table);
}

[Given("the following people")]
public void GivenTheFollowingPeople(Table table)
{
    var people = _xmodels.CreateModels<Person>(table);
    // or, for a specific named builder, applied to EVERY row:
    var dutchPeople = _xmodels.CreateModels<Person>(table, "dutch-person");
}
```

For SpecFlow this is identical, only with `using XModelBuilder.SpecFlow;` and a step parameter
of type `TechTalk.SpecFlow.Table` instead of `Reqnroll.Table`.

Implementation detail: both `Table` classes (Reqnroll and SpecFlow) have an identical shape -
`Header` (`ICollection<string>`), `Rows` (`IEnumerable` of a `TableRow`/`DataTableRow` that
implements `IDictionary<string,string>`, with both a string and an int indexer). The two
extension files are therefore structurally identical; only the using namespace differs.

## 19. Known limitations

- Constructor selection only considers PUBLIC constructors for the "model constructor" path
  (chapter 15); the `Instantiator` fallback does consider non-public constructors, but then
  without any constructor-argument binding via `With(...)` (all arguments are filled with type
  defaults).
- A deep path that happens to start with a constructor parameter name but contains a dot (e.g.
  `"Address.Street"` with constructor parameter `"address"`) is NOT recognized as a constructor
  argument; only the EXACT, dot-free path (`"Address"`) is recognized that way. See chapter 8.
- Lambda path indexing only supports a SINGLE, CONSTANT, INTEGER index argument; computed or
  variable indices, and multiple indexer arguments, are not supported (`NotSupportedException`).
- `ValueConverter.AddKnownTypeConverter` works PROCESS-WIDE/statically: it is not bound to a
  single `IModelBuilderProvider` instance or to `ModelBuilderOptions`.
- `GetListElementType` (used to determine the element type of a collection member) recognizes
  arrays, `List<T>`/`IList<T>` and interfaces that implement `IList<T>`; for other collection
  types (e.g. a custom `ICollection<T>` without `IList<T>`) the element type falls back to
  `object`, which can lead to unexpected boxing/conversion errors.
- Top-level bare (bracketless) array syntax (`"1,2,3"`) is only supported at the very top level
  of a conversion; nested inside an array or object, square brackets are always required.
- The named-builder-reference syntax (chapter 5/10) applies only to non-string reference types;
  for value types, `string` and `object`, any non-token text works as regular data (consistent
  with before this functionality).
- The "vertical vs. horizontal" detection of Gherkin tables (chapter 18) is INHERENTLY ambiguous
  for a table with exactly two columns: XModelBuilder chooses based on the column header NAMES
  (the configured Field/Value-like conventions in
  `ReqnrollTableExtensions`/`SpecFlowTableExtensions.VerticalTableHeaders`), not based on the
  number of rows. An entity with exactly two properties whose column headers happen to be a
  vertical convention (e.g. `"Field"`/`"Value"` or `"Veld"`/`"Waarde"`) is therefore incorrectly
  interpreted as a vertical table; in that (rare) case, use different column names or a third
  column.
- Tuples (`Tuple<...>`/`ValueTuple<...>`) are NOT supported in the mini language (chapter 9) -
  this is a deliberately deferred, optional extension.
- The faker token syntax `"name(args)"` (chapter 11) reserves that entire pattern (an identifier
  immediately followed by parentheses, ending in `')'`) throughout the ENTIRE mini language, for
  ANY target type - even if no `IFaker` is registered at all. Incidental plain-text data in that
  exact form (without a space, e.g. `"Janssen(Junior)"`) is therefore interpreted as a faker call
  and throws a `KeyNotFoundException` if no faker with that name exists; use the `'@'` escape
  mechanism in that case.
- `IFaker` method overloads are chosen based on the NUMBER of arguments (between the mandatory and
  total number of data parameters, so that optional parameters may be omitted) plus whether each
  argument converges to the parameter type. Of the matching overloads, exact arity wins, otherwise
  the one with the fewest defaults to fill in - this is not a full "best match" like the C#
  compiler (no implicit numeric promotions and the like).
- Deep-path faker tokens (chapter 11) resolve the final segment as a single member: a method, or -
  if there is no method and no arguments - a property/field. A method-then-property as the last
  step (e.g. `x.currency().code`) is therefore NOT expressible as a token; use the typed route for
  that.
- `ModelBuilderProviderExtensions.BuildMany` (on the provider) builds each instance via a FRESH
  builder (`provider.For<TModel>()`, optionally with a name); this form therefore NEVER shares
  pre-configuration between instances. If you want that, use the `BuildMany` variant on
  `IModelBuilder<TModel>` itself (chapter 12), which explicitly reuses the SAME builder.
- Faker visibility (chapter 11) is enforced via reflection for the TOKEN route (Public|NonPublic,
  excluding private and generic). For the TYPED route (`Faker<TFaker>()`/constructor injection),
  the ordinary C# accessibility rules of the LANGUAGE itself apply - a protected/private member is
  then already not callable from an external call site, without XModelBuilder having to do or check
  anything for it.

## 20. Specification summary (for reimplementing this framework)

Anyone who wants to reimplement this framework (in the same or another language) essentially needs
these building blocks, in this dependency order:

1. A character scanner with `Peek`/`Next`/`Expect`/`SkipWhitespace`/`EOF` semantics and error
   messages that show the position and a text fragment around the error (`CharScanner`).

2. A recursive-descent parser on top of (1) that implements the grammar from chapter 9 and returns
   a tree of `string | object[] | Dictionary<string,object>`, with a public entry point for "parse
   top-level array, brackets optional" (`DataParser`/`Parser`).

3. A reflection helper layer (`HelperExtensions`) that:
   - for a `(Type, name)` finds a writable member according to the rules in chapter 7
     (property-with-setter, then three backing-field patterns),
   - determines the element type of an array/list/IList-like type,
   - grows a list to a given length with defaults or provider-built elements,
   - offers get/set on a `MemberInfo` uniformly (property or field),
   - extracts the "shallow" property name from a lambda expression (for constructor-argument
     detection),
   - couples a builder class to its (optional) name attribute (for "is this the default builder?" /
     "does this builder have name X?").

4. An "always create me an instance" routine (`Instantiator`) that first looks for a parameterless
   constructor (also non-public), otherwise chooses the constructor with the fewest parameters and
   fills it with type defaults, and on failure falls back to a way of allocating an object without
   running a constructor (in .NET: `RuntimeHelpers.GetUninitializedObject`).

5. A name attribute (such as `ModelBuilderAttribute`) with which a builder class gets a MANDATORY,
   per-model-type UNIQUE name, plus an order-independent "designate the default" step (such as
   `UseAsDefaultModelBuilder`) and a validation that enforces uniqueness and the existence of a
   default (with ≥2 builders), used by (10) to determine which is "the" builder when multiple
   builders are registered for the same model type, and to support explicit name-based lookups.

6. A value converter (`ValueConverter`) that implements the algorithm steps from chapter 10: three
   tokens (`null()`/`new()`/`default()`) plus their escape mechanism (a single leading `'@'`
   character), a regex recognition of the `"name(args)"` faker-call pattern, named-builder
   references for complex types, array/list conversion (with recursion via (2) and itself, including
   `HashSet<T>`/`ISet<T>` as an alternative target shape), `Dictionary<,>`/`IDictionary<,>`
   conversion from the object-literal syntax, and object-literal conversion (build an empty instance
   via the builder provider, fill members based on (3), recursively).

7. An overload-resolution routine (`FakerInvoker`) that, given a list of registered "faker"
   instances (instances of classes that implement an empty marker interface), a name and raw,
   not-yet-converted arguments, plus the `IServiceProvider` of the calling provider: searches the
   list from BACK TO FRONT for the first instance with a non-private, non-generic method of that
   name (case-insensitive; that instance "wins" completely - no mixing of overloads between
   instances), within it chooses the first overload whose number of parameters (any LEADING
   parameters of type `System.Type` and/or `IServiceProvider`, in any order relative to each other,
   not counted - they automatically receive the target type resp. the `IServiceProvider`) matches
   and whose every argument successfully converts to the parameter type via (6), calls the method,
   and converts the result back to the final target type if needed. Also offer a TYPED counterpart
   (`Faker<TFaker>()`) that simply returns the registered `TFaker` instance (no reflection needed -
   ordinary DI resolution/direct instance), so that the same fakers are also callable with full
   compile-time checking; accessibility of INDIVIDUAL methods is enforced for this route by the
   language itself (a private/protected method is already not callable from an external call site).

8. Two "deep-path" appliers that, given a target object and a path (a string with dot/bracket
   notation, or an expression tree), descend member by member according to the rules in chapter 7,
   using (3) and (6). For the lambda variant, also offer a form where the value factory receives the
   active provider as an argument (instead of having to close over it from an enclosing scope), for
   correct reusable factory functions under scoped/parallel providers.

9. A generic builder base class that:
   - selects the constructor on first use per model type (chapter 15),
   - routes `With` calls to constructor-argument storage or to a deep-path settings list (chapter
     8), with a separate `"WithBuilder"` path (lambda + name) to avoid the same ambiguity that a
     generic `With(getter,string)` overload would produce as soon as the member type is itself
     `string`. Constructor-argument values that are strings are NOT replaced in place by their
     converted result (caching would make re-tokenization/randomization impossible on repeated
     `Build()`) - reconvert them on every retrieval, regardless of whether the parameter type is
     itself `string`,
   - on `Build()` first creates an instance (via the standard activator, via the selected
     constructor with the retrieved arguments, or via (4) if there is no usable constructor), and
     then applies all deep-path settings via (8),
   - offers a "build it N times, on the SAME builder instance" convenience method (`BuildMany`) that
     simply calls `Build()` N times - value factories and string-path tokens are then automatically
     re-evaluated N times, literal values remain shared.

10. A provider layer that, given a model type (and optionally a name), returns a corresponding
    builder, with support for MULTIPLE registered builders per model type, ORDER-INDEPENDENTLY: with
    exactly one builder, that single one; with several, the default configured via (5) (and a clear
    error if it is missing - no "last one wins"); and with none, a generic fallback builder (an open
    generic `"DefaultBuilder<T>"` without defaults of its own). The same layer also manages the list
    of registered fakers for (7), exposes that faker-call capability via an INTERNAL-only interface
    (NOT on the public provider contract itself, to keep it tight - the value converter from (6)
    does a runtime type check against that internal interface and falls back gracefully to a clear
    error if a

    an alternative provider implementation does not offer it), and offers a "build N of them,
    each with a fresh builder, optionally with a specific name and/or a per-index configuration
    function" convenience method (`BuildMany` on the provider, distinct from `BuildMany` on the
    builder from (9)). Offer both a DI integration (based on "request all registered
    implementations for a service type", e.g. .NET's `GetServices`) and a DI-free, static
    singleton variant - preferably by having the latter simply manage its own, lazily
    (re)built container and DELEGATE ALL resolution logic to the same DI implementation, instead
    of maintaining a second, standalone resolution implementation - plus a method to resolve
    EXPLICITLY by name (with a clear error on an unknown name, both for builders and for fakers).

11. One or more thin integration layers that normalize a framework-specific "table"
    representation (column headers + rows of string values) into one or more rows of name/value
    pairs, and feed those into (9)'s `WithValues` mechanism - with a heuristic that distinguishes
    between a vertical "field/value" table (column header NAMES match a known convention) and a
    horizontal table (column headers = field names, one row per instance). Deliberately split
    "build one instance" (hangs off an ALREADY REQUESTED builder, for consistency and to be able
    to share pre-configuration) and "build a list" (hangs off the PROVIDER, because it needs its
    own fresh builder per row) across two different anchor types - the same trade-off as with
    (9)/(10).

By implementing and testing these eleven building blocks in this order (preferably with the test
cases given as examples in this document: constructor-only properties, init-only properties,
private backing fields, array/list indexing, nested object literals, tokens, faker calls with and
without Type/IServiceProvider auto-injection and overloading, faker visibility rules, typed faker
calls, Dictionary/HashSet conversion, multiple builders per model type with name resolution,
BuildMany on both builder and provider, culture-specific parsing, and both Gherkin table shapes) you
arrive at a functional equivalent of XModelBuilder including its Gherkin integrations.

## 21. Deterministic generation with a seed (XFaker and BogusFaker)

XModelBuilder itself is fully deterministic: given the same `With` calls, `Build()` always produces
the same model. The ONLY source of randomness is your `IFaker` methods (chapter 11). "Deterministic
generation with a seed" therefore comes down to: seeding the RNG inside your fakers. For that there
are two separate, opt-in packages - keeping the core library dependency-free, just as with
Reqnroll/SpecFlow.

### 21.1 You choose the isolation boundary with `XModelBuilderIsolation`

The provider, the fakers and their seeded RNGs together form the shared, stateful core. How
isolated that is, you decide in ONE place - on `AddXModelBuilder` - with `XModelBuilderIsolation`:

```csharp
public enum XModelBuilderIsolation { Shared, PerScope }
```

- **`Shared`** (default): one shared provider + fakers + seeded RNGs for the whole container
  (Singleton). The DI scope is NOT the boundary; for deterministic tests, build a FRESH
  `ServiceProvider` per test. Two providers with the same seed reproduce each other exactly;
  counters start over per provider. Safe to inject anywhere.
- **`PerScope`**: a fresh provider + fakers + seeded RNGs PER DI scope (Scoped). The scope IS the
  boundary: each scope reseeds, so a BDD scenario per scope is reproducible AND parallel-safe.
  Resolve within a scope; do not inject the provider into a singleton (captive dependency).

```csharp
services.AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope)
        .AddXFaker(seed: 123)
        .AddBogusFaker(seed: 123);

using var scope = root.CreateScope();
var xmodels = scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>();
// everything in this scope shares one seeded set; the next scope gets a fresh one.
```

The choice is a SINGLE knob that sets provider and seeders at the same time, so that the broken
combination (scoped faker + singleton provider) does not exist. And it is ORDER-INDEPENDENT:
`AddXFaker`/`AddBogusFaker` before OR after `AddXModelBuilder` yields the same result (registrations
that arrive too early are deferred and flushed with the correct lifetime once the isolation is
known). `ValidateXModelBuilderRegistrations()` throws if the provider lifetime does not match the
isolation (e.g. by calling `AddXModelBuilder` twice with different isolation).

> Only the provider + fakers + seeded RNGs follow the isolation. `ModelBuilderOptions`, the
> `ModelBuilderDefaults` registry and the builder registrations remain container-wide (the
> `TimeProvider` stays Singleton).

### 21.2 XModelBuilder.Fakers.XFaker - Faker (dependency-free)

The project `XModelBuilder.Fakers.XFaker` contains the class `Faker` (namespace
`XModelBuilder.Fakers.XFaker`): a small, dependency-free faker with deterministic primitives that
Bogus deliberately does NOT do well: identity (counters), order-independent name GUIDs and
clock-bound ages. It receives a seeded `Random` and a `TimeProvider` via the constructor.

```csharp
using XModelBuilder.Fakers.XFaker;

services.AddXModelBuilder()
    .AddXFaker(seed: 12345);   // registers Faker + seeded Random (follows the isolation, chapter 21.1)
```

You can request it in a typed way via `xmodels.Faker<Faker>()`, or more concisely via the
convenience accessor `xmodels.XFaker()` (extension on `IModelBuilderProvider`):

```csharp
var id = xmodels.XFaker().NewGuid("customer-acme");
```

| Token / method | Kind | Notes |
|---|---|---|
| `NextId()` / `NextId(name)` | monotonic counter(s), starting at 1 | unique and readable; named counters are mutually independent |
| `Sequence("INV-{0:0000}")` | readable sequence (INV-0001, ...) | composite format with a counter per format string |
| `NewGuid()` | seeded-random v4 GUID | reproducible given the same seed + call order |
| `NewGuid(name)` | name-based stable GUID (MD5) | same key → same GUID, REGARDLESS of order/parallelism |
| `IntBetween(min,max)` | seeded int (inclusive) | base primitive |
| `Bool(truePercent)` | seeded boolean | true in ~`truePercent`% of cases |
| `DateBetween(min,max)` | seeded date in range | inclusive |
| `AgeBetween(min,max)` / `AgeBetween(min,max,atDate)` | birthdate for an age range | "now" comes from `TimeProvider`, NOT `DateTime.Today` - so also deterministic |

Two kinds of "deterministic", deliberately side by side:

- RNG-based (`NewGuid()`, `IntBetween`, `DateBetween`, `AgeBetween`): reproducible given a seed, but
  the value depends on how many times the RNG has already been drawn (call order).
- Name-based (`NewGuid(name)`): the same key always maps to the same GUID, independent of order or
  parallelism. Preferable when you want a STABLE id for a known entity rather than "just a random id".

```csharp
var person = xmodels.For<Person>()
    .With("Id", "NewGuid(customer-acme)")   // stable per key
    .With("Birthday", "AgeBetween(20,30)")  // reproducible given a seed
    .Build();
```

### 21.3 XModelBuilder.Fakers.Bogus - BogusFaker

The project `XModelBuilder.Fakers.Bogus` contains `BogusFaker` (namespace
`XModelBuilder.Fakers.Bogus`), deliberately minimal: it only exposes the seeded Bogus `Faker` as the
property `Bogus`. The entire Bogus surface is reachable via **deep-path faker resolution** (chapter
11) - there are NO hand-written adapter methods.

```csharp
using XModelBuilder.Fakers.Bogus;

services.AddXModelBuilder()
    .AddBogusFaker(seed: 12345);   // registers BogusFaker + a per-instance seeded Bogus.Faker
```

From tokens you use a member path that starts at the `Bogus` property:

```csharp
.With("Name",  "bogus.name.firstname()")
.With("Email", "bogus.internet.email()")
.With("City",  "bogus.address.city()")
.With("Name",  "bogus.person.firstname()")   // terminal is a property -> read via the fallback
```

The `bogus.` path immediately gives each generator a namespace, so these tokens do not collide with
your own fakers or with the `Faker` faker. For the combinations that deep-path does not cover (e.g.
method-then-property such as `Finance.Currency().Code`) you use the typed route - via
`Faker<BogusFaker>().Bogus` or the convenience accessor `xmodels.Bogus()` (extension on
`IModelBuilderProvider` that returns the underlying Bogus `Faker`):

```csharp
var county   = xmodels.Faker<BogusFaker>().Bogus.Address.County();
// or, instead of xmodels.Faker<BogusFaker>().Bogus, you can use the shorthand extension xmodels.Bogus():
var currency = xmodels.Bogus().Finance.Currency().Code;
```

Bogus uses its OWN randomizer (separate from `System.Random`). `AddBogusFaker` seeds it per instance
via `new Faker { Random = new Randomizer(seed) }` - NOT the global static `Randomizer.Seed`, because
that is process-wide and would make parallel runs bleed into each other.

### 21.4 Using them together

Both fakers can coexist in the same provider; thanks to the `bogus.` path their tokens do not
collide:

```csharp
var provider = new ServiceCollection()
    .AddXModelBuilder()
    .AddXFaker(seed: 2024)
    .AddBogusFaker(seed: 2024)
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();

var person = provider.For<Person>()
    .With("Id", "NewGuid(customer-acme)")      // Faker (stable)
    .With("Name", "bogus.name.firstname()")     // BogusFaker, deep-path
    .With("City", "bogus.address.city()")       // BogusFaker, deep-path
    .Build();
```

### 21.5 Points of attention

- **Banish other ambient non-determinism from your own fakers.** Not only `Random.Shared`, but also
  `Guid.NewGuid()`, `DateTime.Now`/`UtcNow`. Route everything through an injected, seeded `Random`
  and (for time) a `TimeProvider`. A single escaped `Guid.NewGuid()` makes the whole thing
  non-deterministic.
- **Reserved characters in token arguments.** Faker arguments go through the mini-language parser
  (chapter 9), so characters like `:`, `,`, `[`, `]`, `{`, `}` cannot simply appear in a bare
  argument. For a name key with such a character, use a separator like `-`
  (`NewGuid(customer-acme)`) or a string literal (`NewGuid("customer:acme")`).
- **Use the provider form for value factories** (chapter 6, form g) in scenarios with multiple
  providers, so that the factory is guaranteed to get the correct provider - and thus the correct
  seeded faker: `.With(x => x.Address, p => p.Faker<AddressFakers>().Random())`.
