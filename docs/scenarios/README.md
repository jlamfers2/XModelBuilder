# Scenario recipes

Task-oriented walkthroughs that show how to *build a whole dataset* with XModelBuilder and
what you do with it afterwards. They assume you already know the basics (`For<T>()`, `With`,
`Build`/`BuildMany`, fakers). For the full, canonical spec see [`../../README.md`](../../README.md);
the chapter references below point back into it.

Every scenario is **deterministic**: given the same seed (and, where relevant, the same clock)
the dataset reproduces byte-for-byte on every run. That is what makes these datasets usable as
fixtures, golden files, or the backing data for BDD scenarios.

| # | Scenario | Shows off |
|---|---|---|
| [01](01-time-traveling.md) | **Time-traveling** | A fake `TimeProvider` drives clock-bound data (ages, order dates); freeze it, then advance it and watch the same entities age. |
| [02](02-companies-with-addresses.md) | **100 companies with addresses (12% differ)** | `BuildMany` + a `Build()` override + the seeded `Bool(percent)` faker to make a *fraction* of the set behave differently, reproducibly. |
| [03](03-related-graph-customers-orders.md) | **A related graph (customers → orders)** | Parent/child object graphs with stable, name-based GUIDs as keys/foreign keys, and readable running sequence numbers. |
| [04](04-deterministic-dutch-dataset.md) | **A deterministic Dutch dataset** | `DutchFaker` producing *valid* BSN/RSIN/IBAN/postcode/kenteken, seeded so the exact same set comes back every run. |

## The one setup they share

All four register a provider once and reuse it. Under the default `Shared` isolation a fresh
`ServiceProvider` reproduces exactly given the same seed, so for reproducible data you build one
provider per dataset/test (see README chapter 21.1).

```csharp
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder;                 // core
using XModelBuilder.Fakers.XFaker;   // AddXFaker, xfake.* tokens, xprovider.XFake()
using XModelBuilder.Fakers.Bogus;    // AddBogusFaker, bogus.* tokens, xprovider.Bogus()
using XModelBuilder.Fakers.Dutch;    // AddDutchFaker, nl.* tokens, xprovider.NL()

var xprovider = new ServiceCollection()
    .AddXModelBuilder()
    .AddXFaker(seed: 2026)
    .AddBogusFaker(seed: 2026)
    .AddDutchFaker(seed: 2026)
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();
```

> The C# accessors used throughout are `xprovider.XFake()` (returns the `XFakerApi` — `NextId`,
> `NewGuid`, `Bool`, `IntBetween`, `DateBetween`, `AgeBetween`, `Sequence`), `xprovider.Bogus()`
> (the seeded Bogus `Faker`) and `xprovider.NL()` (the Dutch generators). The same generators are
> reachable as string tokens (`"xfake.newguid(acme)"`, `"bogus.address.city()"`, `"nl.bsn()"`) in
> `With(string, string)` and Gherkin tables — see README chapters 10–11 and 21.
