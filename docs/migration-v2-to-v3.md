# Migrating from v2 to v3

v3 redesigns **builder resolution** (see [ADR 0001](adr/0001-default-builder-is-an-always-applied-base-layer.md)
and [ADR 0002](adr/0002-cross-cutting-layer-is-separate-from-the-sealed-base.md), and README chapter 5).
The public API for building, deep paths, the mini data language, fakers and the Gherkin integrations is
**unchanged** — only how a model type maps to a builder changed. Most projects touch only their DI
registration and a handful of call sites.

> **Starting a new project?** You can ignore this document — just follow README chapter 5. This guide is
> only for upgrading an existing v2 codebase.

---

## The one conceptual change

In v2, `For<T>()` could return a **specific** builder: for a model type with a single registered builder
it returned that builder, and for a type with several you first picked one with `UseAsDefaultModelBuilder`.
That made `For<Person>()` opaque — you could not tell *which* builder ran without reading the DI setup.

In v3, resolution is split into transparent, composable layers:

| Call | v3 behaviour |
|---|---|
| `For<T>()` | ALWAYS the fixed base + the optional cross-cutting layer — **never** a specific builder |
| `Use<TBuilder>()` / `For<T>("name")` | base + cross-cutting layer, then that specific builder on top |
| `ForEmpty<T>()` | the bare base only (cross-cutting layer skipped) |

So a specific builder now runs **only when you name it**. There is no "default among several builders" to
configure, and no hidden custom builder behind `For<T>()`.

---

## API changes

| v2 (removed) | v3 (replacement) |
|---|---|
| `services.AddDefaultModelBuilder(typeof(DefaultModelBuilder<>))` | **nothing** — `AddXModelBuilder()` auto-registers the fixed, sealed base |
| `services.UseAsDefaultModelBuilder<PersonBuilder>()` | delete it; call `Use<PersonBuilder>()` (or `For<Person>("name")`) at the point of use |
| `For<Person>()` returning a specific `PersonBuilder` | `Use<PersonBuilder>()` — `For<Person>()` is now base + cross-cutting |
| a shared open-generic default builder for cross-cutting fields | `services.AddCrossCuttingModelBuilder(typeof(EntityDefaults<>))` |
| standalone `DefaultModelBuilderProvider.Current.SetDefaultModelBuilder(...)` | `DefaultModelBuilderProvider.Current.AddCrossCuttingModelBuilder(...)` |
| standalone `...Current.UseAsDefaultModelBuilder<T>()` | delete it; call `Use<T>()` / `For<T>("name")` at the point of use |

`AddModelBuilder<T>()`, `[ModelBuilder("name")]` (still mandatory + unique per model type),
`AddModelBuildersFromAssembly/Assemblies`, `ValidateXModelBuilderRegistrations()`, `ForEmpty<T>()`, all
`With`/`WithValues`/`Build`/`BuildMany`/`Extend` methods, fakers and the Gherkin integrations are
**unchanged**.

---

## Step by step

1. **Remove `AddDefaultModelBuilder(...)`.** The base is registered for you by `AddXModelBuilder()`.
2. **Delete every `UseAsDefaultModelBuilder(...)` call** (DI and standalone).
3. **Fix the call sites that relied on `For<T>()` returning a specific builder.** Change them to
   `Use<TThatBuilder>()` (compile-time-checked, refactor-safe) or, in text-driven contexts,
   `For<T>("name")`.
   ```csharp
   // v2
   var person = xprovider.For<Person>().Build();          // returned PersonBuilder
   // v3
   var person = xprovider.Use<PersonBuilder>().Build();   // explicit specific builder
   ```
4. **Move cross-cutting defaults into a cross-cutting layer.** If you used a shared open-generic default
   builder for fields every model should get (a deterministic `Id`, a tenant, audit fields), register it
   with `AddCrossCuttingModelBuilder`:
   ```csharp
   public sealed class EntityDefaults<TModel>(IOptions<ModelBuilderOptions> o, IModelBuilderProvider p)
       : ModelBuilder<EntityDefaults<TModel>, TModel>(o, p) where TModel : class
   {
       protected override void SetDefaults()
       {
           // guard: only types that actually have a Guid Id
           if (typeof(TModel).GetProperty("Id")?.PropertyType == typeof(Guid))
               With("Id", "xfake.NewGuid()");
       }
   }

   services.AddCrossCuttingModelBuilder(typeof(EntityDefaults<>));
   ```
   It runs on every build (except `ForEmpty`), at the LOWEST precedence, so a specific builder or an
   explicit `With` still overrides it. The demo wires exactly this pattern with an audit `CreatedAt`
   (see [`EntityDefaults.cs`](../Demo/XModelBuilder.Demo.Shop.IntegrationTests/Support/EntityDefaults.cs)).
5. **Run `ValidateXModelBuilderRegistrations()`** after all registrations. The "≥2 builders must have a
   configured default" rule is gone; the "every builder has a unique `[ModelBuilder(name)]` per model
   type" rule stays.

---

## Behaviour changes to watch for

- **`For<T>()` on a type with one custom builder.** In v2 this returned that builder; in v3 it is the
  base + cross-cutting layer. Call `Use<TBuilder>()` where you meant the specific builder.
- **Nested auto-vivification, the `default()` token and `WithDefault<T>`** all build through `For(type)`
  with no name, so they now resolve to the base + cross-cutting layer for that type (the predictable
  outcome). To fill a nested member through a SPECIFIC builder, set it explicitly with `WithBuilder` or
  `Use<TBuilder>().Build()` (README chapters 5–6).
- **No per-builder opt-out of the cross-cutting layer.** A specific builder can only *override* a
  cross-cutting value (last-wins), not prevent the layer from running. For a truly pristine instance that
  opts out of everything, use `ForEmpty<T>()`.

---

## See also

- README chapter 5 — builder resolution (the base, the cross-cutting layer, named builders).
- [ADR 0001](adr/0001-default-builder-is-an-always-applied-base-layer.md) /
  [ADR 0002](adr/0002-cross-cutting-layer-is-separate-from-the-sealed-base.md) — the reasoning.
- [`testing-best-practices.md`](testing-best-practices.md) — how the layers fit a real test suite.
