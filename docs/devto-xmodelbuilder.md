---
title: Stop writing a test-data builder for every class in .NET
published: true
tags: dotnet, testing, csharp, bdd
canonical_url: https://dev.to/jlamfers2/stop-writing-a-test-data-builder-for-every-class-in-net-4k6n
---

If you've ever written test data by hand, you know the ritual: a `PersonBuilder`, an
`OrderBuilder`, an `AddressBuilder`… one hand-written builder per class, each one a wall of
`WithX(...)` methods you have to maintain forever. The Test Data Builder and Object Mother
patterns are great — the boilerplate is not.

**XModelBuilder** gives you a fluent builder for *any* C# class out of the box. No per-class
builder required. It handles constructor parameters, init-only properties, read-only members,
even private backing fields — via reflection, deterministically.

## Install

```
dotnet add package XModelBuilder
```

## 30-second example

You can use it fully standalone (no DI container) through a small static facade:

```csharp
using XModelBuilder.Default;

var order = For.Model<Order>()
    .With(x => x.OrderDate, new DateTime(2026, 7, 1))
    .With(x => x.Lines[0].Product, "Widget")   // deep paths + indexers just work
    .With(x => x.Lines[0].Quantity, 3)
    .Build();
```

No `OrderBuilder`, no `OrderLineBuilder`. The `Lines[0].Product` path drills into a nested
collection element and sets it for you. Need a whole list? `Create.Models<Order>(10)`.

## Deterministic fakers, seeded once

Random test data that changes every run is a debugging nightmare. XModelBuilder ships a
seeded, dependency-free faker (and a Bogus integration if you prefer). Register it once:

```csharp
services.AddXModelBuilder()
    .AddXFaker(seed: 12345);   // reproducible values, every run
```

Then let it fill in the noise while you set only what your test actually cares about:

```csharp
var order = xprovider.For<Order>()
    .With(x => x.Id, p => p.XFake().NewGuid())
    .With(x => x.Customer.Name, p => p.Bogus().Company.CompanyName())
    .With(x => x.Lines[0].Quantity, 3)
    .Build();
```

`XFake().NewGuid("customer-acme")` even gives you a *stable* GUID from a name — same key, same
GUID, regardless of call order or parallelism. Deterministic by design.

## Build a whole list: `BuildMany`

Need ten of something, each slightly different? `BuildMany` reuses one builder and
re-evaluates the *varying* parts on every instance, while keeping the shared bits fixed:

```csharp
var people = xprovider.For<Person>()
    .With(p => p.City, "Amsterdam")                     // shared by all 10
    .With(p => p.Name, p => p.Bogus().Name.FullName())  // 10 different names
    .BuildMany(10);
```

Prefer to vary explicitly by index? There's an overload for that:

```csharp
var people = xprovider.BuildMany<Person>(5, (b, i) => b
    .With(p => p.Name, $"Person{i}"));
```

Standalone (no DI) it's a single call: `Create.Models<Person>(10)`.

## Give a class sensible defaults (optional)

When you *do* want per-type defaults, you write a tiny builder — and only the defaults, nothing
else:

```csharp
[ModelBuilder("person")]
public sealed class PersonBuilder(
    IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<PersonBuilder, Person>(options, xprovider)
{
    protected override void SetDefaults() => WithDefault(p => p.Address); // Address fills itself
}
```

There's no framework-wide "build the whole graph" recursion to fight: each type fills its own
level, and a back-reference is simply a default you don't write. No cycle guards needed.

## More than one recipe per type: named builders

Sometimes you want several presets for the *same* class — a minimal one and a fully-populated
one, say. Give each builder a unique name via `[ModelBuilder("...")]`:

```csharp
[ModelBuilder("complex-address")]
public sealed class ComplexAddressBuilder(
    IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<ComplexAddressBuilder, Address>(options, xprovider)
{
    protected override void SetDefaults() => With(x => x.Street, "Main Street");
}
```

Register as many as you like and designate which one is the default (no magic strings — the
model type is derived from the builder):

```csharp
services
    .AddModelBuilder<ComplexAddressBuilder>()    // [ModelBuilder("complex-address")]
    .AddModelBuilder<SimpleAddressBuilder>()      // [ModelBuilder("simple")]
    .UseAsDefaultModelBuilder<SimpleAddressBuilder>();
```

`For<Address>()` now uses the default. In C# you'll normally reach for a specific builder in a
**typed** way — compiler-checked, refactor-safe, no magic string:

```csharp
var fancy = xprovider.Use<ComplexAddressBuilder>().Build();
var five  = xprovider.Use<ComplexAddressBuilder>().BuildMany(5);
```

The *string* name (`"complex-address"`) is really there for the places where you have **no type
at hand** — first and foremost Gherkin tables and the mini data language. In a table, a cell for
an `Address` member simply reads `complex-address`, and because the target is a reference type it
resolves to that named builder automatically:

```gherkin
| Customer.Name | ShippingAddress |
| Jane Smith    | complex-address |
```

(Need the literal text instead of a builder? Escape it: `@complex-address`.)

That's the theme of the whole untyped layer: faker tokens, deep-path strings and named-builder
references exist to make **text-driven** sources like Gherkin first-class. In plain C# you stay
strongly typed; in a feature file you get the same power from plain text.

## Gherkin tables become objects

This is where it clicks for BDD. A Reqnroll/SpecFlow table maps straight onto a model —
dot-paths, indexers, type conversion and faker tokens all work *inside the table*:

```gherkin
Given the following order:
  | Id              | Customer.Name | Lines[0].Product | Lines[0].Quantity |
  | xfake.NewGuid() | Jane Smith    | Widget           | 3                 |
```

```csharp
[Given("the following order:")]
public void GivenTheFollowingOrder(Table table)
    => _order = _xprovider.For<Order>().CreateModel(table);
```

Your feature file *is* your test data. It auto-detects the two common table shapes
(vertical `Field | Value` and horizontal), and the column headers for the vertical shape are
configurable per language.

## Why I like it

- One generic base class builds every model — no hand-written builders required.
- Deterministic: seeded fakers, name-based stable GUIDs, `TimeProvider`-driven dates.
- Works with `Microsoft.Extensions.DependencyInjection` *or* fully standalone.
- A mini data language turns plain strings into arrays, dictionaries and nested objects.

If you write a lot of tests, it removes a whole category of maintenance.

- GitHub: https://github.com/jlamfers2/XModelBuilder
- NuGet: https://www.nuget.org/packages/XModelBuilder

Feedback and stars welcome — it's MIT-licensed.
