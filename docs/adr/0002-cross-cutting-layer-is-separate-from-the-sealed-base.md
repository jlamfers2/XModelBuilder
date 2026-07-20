# ADR 0002 — The cross-cutting layer is a separate registration; the base builder is sealed

- **Status:** Accepted — 2026-07-13
- **Amends:** [ADR 0001](0001-default-builder-is-an-always-applied-base-layer.md) (which stays Accepted;
  this ADR refines *how* the always-applied layer is registered and named)

## Context

ADR 0001 made "the default builder" an always-applied base layer: `For<T>()` runs it, specific
builders layer on top, `ForEmpty<T>()` opts out. It implemented this by making the keyed `"default"`
open-generic slot serve **two** roles at once:

1. the **fallback base** for model types with no dedicated builder (out of the box the do-nothing
   `DefaultModelBuilder<>`), and
2. the **cross-cutting defaults** vehicle — you *replaced* that same slot (`ApplyToEveryModel(...)`)
   to give every model shared defaults (e.g. a deterministic Guid `Id`).

That is the very overloading ADR 0001 set out to remove (it removed "the default among many" because
"default" meant two things). But "the default builder" still meant two things: the fallback base
**and** your cross-cutting layer. Consequences:

- `DefaultModelBuilder<>` was *replaceable*, so a reader could not assume it was a no-op — "does the
  default builder secretly do something here?" was back.
- `ForEmpty<T>()` looked pointless in the common case, because the layer it opts out of does nothing
  by default; its reason to exist only appeared once you replaced the slot.
- `ApplyToEveryModel` read as "reconfigure the default builder" rather than "add a cross-cutting
  aspect."

## Decision

Split the two roles into two distinct, single-purpose slots, and finish de-overloading "default."

- **The base** is the fixed, **sealed** `DefaultModelBuilder<>` (keyed `"default"`). It does nothing,
  and is **not user-replaceable**. It is what `For<T>()` and `ForEmpty<T>()` construct, and what a
  model type without a dedicated builder resolves to.
- **The cross-cutting layer** is a **separate** registration in its own keyed slot
  (`"crosscutting"`), registered with the renamed verb **`AddCrossCuttingModelBuilder(typeof(...))`**
  (standalone: `DefaultModelBuilderProvider.Current.AddCrossCuttingModelBuilder(...)`). It is seeded
  into every build at the lowest precedence — exactly the `Reset()`/`SeedFromLayer` mechanism from
  ADR 0001 — but it no longer shadows or replaces the base.

### Resolution semantics (unchanged in effect, clearer in structure)

| Call | What runs |
|---|---|
| `For<T>()` | the base + the cross-cutting layer |
| `Use<TBuilder>()` / `For<T>("name")` | the base + the cross-cutting layer, then that specific builder, then user config |
| `ForEmpty<T>()` | the bare base only — the cross-cutting layer is skipped |

The built object is identical to ADR 0001's model; the difference is structural: `For<T>()` now
returns the sealed base with the cross-cutting layer seeded in, rather than returning the
(replaceable) cross-cutting builder itself.

### Naming

`ApplyToEveryModel` → **`AddCrossCuttingModelBuilder`**. It joins the `Add*` registration family
(`AddModelBuilder`, `AddFaker`), and — crucially — it does **not** contain the word "default," so the
cross-cutting layer never blurs back into "the default builder." `UseAsDefaultModelBuilder` and the
`ModelBuilderDefaults` registry remain gone (from ADR 0001).

### `ForEmpty<T>()` is kept

Because the cross-cutting layer can now genuinely do something on `For<T>()`, `ForEmpty<T>()` regains
a crisp, symmetric purpose: *`For` includes the cross-cutting layer; `ForEmpty` excludes it.* It
builds the sealed base with the layer suppressed.

## Consequences

**Positive**

- `DefaultModelBuilder<>` is a guaranteed no-op (sealed, non-replaceable) — a reader never wonders
  whether "the default builder" was swapped for something that sets fields.
- The cross-cutting layer is a single-purpose, clearly-named, discoverable registration (one call in
  the DI setup), which serves the transparency goal: no one inherits a build and cannot tell what is
  set where — cross-cutting defaults are one place, overridable, and `ForEmpty` opts out.
- `ForEmpty<T>()` has an obvious reason to exist again.

**Neutral / cost**

- One extra internal keyed slot (`"crosscutting"` alongside `"default"`), invisible to users. The
  learnable surface is unchanged: an always-applied layer + specific builders.
- The recursion guard now suppresses re-entrancy while the *cross-cutting* layer for a type is being
  constructed (and while `ForEmpty` builds), instead of while the merged default layer was.

**Explicitly out of scope (considered, rejected for now)**

- A **post-build** cross-cutting hook (a notifier/mutator on the finished object). Rejected: it is a
  different contract (runs last, can override explicit values), which is in tension with the
  transparency goal, and per-builder computed defaults are already covered by a `Build()` override +
  `SetMember` (README chapter 13). If a concrete cross-cutting-post need appears, it should be a
  separate, explicitly-named mechanism (`IPostBuildHook`), never a boolean flag on
  `AddCrossCuttingModelBuilder`.

## Implementation notes

- `AddXModelBuilder` registers the sealed `DefaultModelBuilder<>` under keyed `"default"` directly;
  `AddCrossCuttingModelBuilder` registers its open generic under keyed `"crosscutting"`.
- `ModelBuilderProvider.For(Type)` resolves the `"default"` base; the internal
  `ICrossCuttingLayerProvider.GetCrossCuttingLayer(Type)` resolves the `"crosscutting"` slot (null if
  none). `ModelBuilder.Reset()` seeds the latter before its own `SetDefaults()`.
- README/README-nl chapter 5 (and the ch. 6 `WithDefault`, ch. 13/14 and interface listings) describe
  the base + cross-cutting split.
