# ADR 0001 — The default builder is an always-applied base layer

- **Status:** Accepted — 2026-07-13
- **Supersedes:** the per-model "default among many" resolution (README chapter 5, rule 3)
- **Refined by:** [ADR 0002](0002-cross-cutting-layer-is-separate-from-the-sealed-base.md) — the
  always-applied layer is registered as a SEPARATE cross-cutting slot (`AddCrossCuttingModelBuilder`)
  rather than by replacing the now-sealed `DefaultModelBuilder<>` base. Where this ADR says
  `ApplyToEveryModel`, read `AddCrossCuttingModelBuilder`.

## Context

Today "default builder" means two different things, and the overlap is confusing:

1. **The generic fallback** — the open-generic `DefaultModelBuilder<>` registered (keyed `"default"`)
   via `AddDefaultModelBuilder(typeof(...))`. Used **only** for model types that have *no* dedicated
   builder.
2. **The default among several** — when one model type has ≥2 builders, `UseAsDefaultModelBuilder<T>()`
   records which one `For<T>()` (no name) returns; without it, resolution throws.

Meaning (2) makes `For<Person>()` opaque at the call site: you cannot tell *which* builder runs
without going to read the DI configuration. It is deterministic (order-independent, throws on
ambiguity), but it is not **transparent**. It also collides conceptually with (1): both are "the
default".

Separately, teams want **cross-cutting defaults** — e.g. "every entity gets a deterministic Guid
`Id`". The natural fix (a shared base class whose `SetDefaults()` sets the Id) has two problems: the
CRTP self-type of `ModelBuilder<TBuilder,TModel>` makes deriving a concrete builder from
`DefaultModelBuilder<T>` awkward, and inheritance is *forgettable* — a new builder that omits
`base.SetDefaults()` silently loses the Id.

## Decision

Collapse both meanings into one simple, transparent model: **the registered default builder is a
base layer that is applied to *every* build. Specific builders are applied *on top* of it.**

### Resolution semantics

| Call | What runs |
|---|---|
| `For<T>()` | the default layer only |
| `Use<TBuilder>()` | the default layer, then `TBuilder`'s layer, then user config |
| `For<T>("name")` | the default layer, then the `[ModelBuilder("name")]` builder's layer, then user config |
| `ForEmpty<T>()` | **nothing** — a bare instance, default layer skipped |

The mental model is a single sentence: *"my default builder always runs; calling a specific builder
runs it in addition; `ForEmpty` opts out of everything."* Nothing is chosen invisibly — `For<T>()` is
always the default layer, never a surprise custom builder.

### Layering is one pipeline (constructor arguments preserved)

`Use<TBuilder>()` does **not** build twice. All layers contribute into **one** build pipeline:

1. The default builder's `SetDefaults()` settings are applied first.
2. Then the specific builder's `SetDefaults()` settings.
3. Then the caller's `With`/`WithValues`.
4. The instance is constructed **once** from the merged constructor-argument set, then the remaining
   deep-path settings are applied.

Later layers override earlier ones on the same target (last-wins), exactly as overlapping `With`
calls do today. Because *all* constructor-argument contributions are collected **before**
construction, a specific builder can still influence construction (ctor-only / immutable models keep
working). This is option **(A)** from the design discussion; option (B) — "default constructs,
specific `Extend`s on top" — was rejected because it would strip specific builders of their ability
to supply constructor arguments.

### The registration is renamed to a verb that states the behavior

The type keeps its name — `DefaultModelBuilder<>` still *provides the defaults*, and with meaning (2)
removed the word "default" is no longer overloaded. What changes is the **registration**, so that the
"runs for every model" semantics are legible where you configure it:

```csharp
// before
services.AddDefaultModelBuilder(typeof(DefaultModelBuilder<>));

// after — the verb says what it does
services.ApplyToEveryModel(typeof(DefaultModelBuilder<>));
```

The standalone facade (`DefaultModelBuilderProvider`) renames `SetDefaultModelBuilder(...)` to the
same verb.

> **Naming is provisional.** `ApplyToEveryModel` is chosen for maximum transparency; `UseForEveryModel`
> or `AddBaseModelBuilder` are equivalent candidates. `AddBaseModelBuilder` is weaker because "base"
> collides with the abstract base class `ModelBuilder<,>`.

### Cross-cutting defaults become trivial and unforgettable

A team-wide default (Guid `Id`, tenant, audit fields) is written **once**, as the registered default
builder, and it can never be forgotten because it always runs:

```csharp
public sealed class EntityDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<EntityDefaults<TModel>, TModel>(options, xprovider)   // closes TBuilder = itself; no CRTP snag
    where TModel : class
{
    protected override void SetDefaults()
    {
        if (HasWritableGuidId(typeof(TModel)))       // guard: only types that actually have a Guid Id
            With("Id", "xfake.NewGuid()");
    }
}

services.ApplyToEveryModel(typeof(EntityDefaults<>));
```

No inheritance requirement, no `base.SetDefaults()` call to forget, no CRTP awkwardness.

### What is removed

- `UseAsDefaultModelBuilder<T>()` / `UseAsDefaultModelBuilder(Type)` (DI **and** the standalone
  facade).
- The `ModelBuilderDefaults` registry.
- The resolution branch "≥2 builders → look up the configured default (else throw)".
- The validation rule "every model type with ≥2 builders must have a configured default".

### What stays

- `[ModelBuilder("name")]`, still **mandatory and unique per model type** — names remain the address
  for text-driven contexts (Gherkin tables, the mini data language) and for `For<T>("name")` /
  `Use<TBuilder>()`.
- `AddModelBuilder<T>()` — registers a builder reachable via `Use<T>()` or `For<T>("name")`.
- `ForEmpty<T>()` — unchanged: constructs the stock `DefaultModelBuilder<T>` directly with no layer,
  the escape hatch for a truly pristine instance.
- Multiple builders per model type — allowed and unremarkable now; they are simply
  name-/type-addressable specifics, with no "default among them" to configure.

## Consequences

**Positive**

- `For<T>()` is fully predictable: always the default layer, never a hidden custom builder.
- Resolution logic collapses (`ModelBuilderProvider.For` no longer consults `ModelBuilderDefaults`;
  no ≥2 branch).
- Cross-cutting defaults are one place and impossible to forget.
- One-sentence mental model for the whole default/specific/empty story.

**Negative / trade-offs**

- **Loss of per-builder opt-out.** With inheritance a subclass could decline `base.SetDefaults()`.
  Now a specific builder can only *override* a default value (last-wins), not *prevent* the default
  layer from running. If the default sets something as a constructor argument that a caller wants
  gone entirely, `ForEmpty<T>()` (which drops *all* layers) is the only escape.
- **Behavior change for single-custom-builder types.** Today `For<Person>()` returns the one
  registered `PersonBuilder`. After this change `For<Person>()` is the default layer; to get
  `PersonBuilder` you call `Use<PersonBuilder>()`. This is more consistent (For = generic, Use =
  specific) but it is a breaking change.
- **More framework-internal composition.** The base layer is composed by the framework (seed the
  pipeline with the default builder's settings before the specific builder's `SetDefaults()`), rather
  than by the CLR calling `base`. See implementation notes.

**Neutral**

- Nested auto-vivification, the `default()` token and `WithDefault<T>` all call `For(type)` with no
  name, so they now resolve to the default layer — which is exactly the predictable outcome. To build
  a nested member with a *specific* builder, set it explicitly (`Use<TBuilder>()` / `WithBuilder`).

## Implementation notes

The one non-trivial part is composing the default layer *before* a specific builder's `SetDefaults()`
while keeping a single construction. `SetDefaults()` runs from the constructor via `Reset()`, so the
base layer must be injected at that point. Sketch:

- `ModelBuilder.Reset()` gains a hook that first applies the **default layer** (the settings produced
  by the registered `ApplyToEveryModel` open-generic for this model type), then calls the builder's
  own `SetDefaults()`.
- For `For<T>()` there is no additional builder, so only the default layer runs.
- For `ForEmpty<T>()` the hook is skipped entirely (as `ForEmpty` already bypasses the keyed default).
- The default layer must be resolved without infinite recursion (the default builder itself must not
  re-apply the default layer).
- The default layer's contributions carry the *lowest* precedence, so any later `With` — from a
  specific builder or the caller — overrides them.

`ValidateXModelBuilderRegistrations()` keeps the "names are unique per model type" and "every builder
has a `[ModelBuilder]` name" checks; it drops the "≥2 needs a default" check.

## Migration

- Delete all `UseAsDefaultModelBuilder(...)` calls. Where code relied on `For<T>()` returning a
  specific builder, change those call sites to `Use<TThatBuilder>()`.
- Rename `AddDefaultModelBuilder(...)` → `ApplyToEveryModel(...)` and the standalone
  `SetDefaultModelBuilder(...)` likewise.
- Update README/README-nl chapter 5 (resolution) and chapter 14 (standalone) accordingly.

## Alternatives considered

- **Keep `UseAsDefaultModelBuilder`.** Rejected: it is deterministic but not transparent, and it is
  the source of the "which builder does `For<T>()` run?" confusion this ADR removes.
- **Cross-cutting defaults via a shared abstract base class.** Rejected as the primary mechanism: the
  CRTP self-type makes it awkward, and it is forgettable (`base.SetDefaults()` can be omitted). The
  always-applied layer supersedes it. (Inheritance remains available to anyone who wants it; it is
  simply no longer required.)
- **Rename the type to `AutoExecBuilder` / `AlwaysAppliedModelBuilder`.** Rejected: with meaning (2)
  gone, `DefaultModelBuilder` is no longer overloaded and still accurately means "provides the
  defaults"; the `For` / `ForEmpty` pair already encodes "layer runs / does not run". The
  transparency belongs in the **registration verb**, not the type name.
- **Option (B): default constructs, specific `Extend`s.** Rejected: strips specific builders of the
  ability to supply constructor arguments (immutable / ctor-only models).
