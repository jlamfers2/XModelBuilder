# Testing best practices with XModelBuilder

*(Nederlandse versie: [`testing-best-practices-nl.md`](testing-best-practices-nl.md).)*

A practical, opinionated guide to standing up **unit tests** and **integration tests** for a large,
professional project, using XModelBuilder as the single source of deterministic test data. It covers
how to structure the suites, do's and don'ts, and the concrete building blocks — domain-grouped step
definitions, drivers, scenario contexts, storage helpers, isolated database transactions — plus the
modern practices that keep a big suite fast and maintainable.

The worked reference throughout is the demo web shop under
[`Demo/XModelBuilder.Demo.Shop.IntegrationTests`](../Demo/README.md); file names in code comments below
point at real files there. Read this document together with README chapter 5 (builder resolution),
chapter 12 (`BuildMany`), chapter 18 (Reqnroll/SpecFlow + `Extend`) and chapter 21.1 (isolation).

---

## 0. TL;DR — the eleven rules

1. **Test data has ONE owner: a builder.** Never hand-`new()` a domain object in a test; build it.
2. **Deterministic by default.** Fixed seed, fixed clock (`TimeProvider`); no `DateTime.Now` or
   `Guid.NewGuid()` in code you assert on — replace those with deterministic defaults (a seeded faker,
   and a Guid/audit stamp in the cross-cutting layer).
3. **`Use<TBuilder>()` / `For<T>("name")` for specifics; `For<T>()` for the plain base; `ForEmpty<T>()`
   to opt out of cross-cutting defaults.** Know which one you mean (README ch. 5).
4. **One behaviour per test.** Arrange-Act-Assert, or Given-When-Then. No "and also" tests.
5. **Structure first by DOMAIN, then by artifact.** Discoverability beats cleverness.
6. **Steps are thin; drivers do the work; contexts carry state.** A step is a sentence, not a program.
7. **Isolate every test.** Unit tests share nothing; integration scenarios reset to a committed seed
   via a rolled-back transaction.
8. **Assert behaviour, not implementation.** Prefer the public API / HTTP surface over poking the DB.
9. **Fast feedback wins.** In-process host, one expensive setup per run, cheap per-scenario reset.
10. **Kill flakiness on sight.** A flaky test is a broken test; quarantine and fix, never `[Retry]`.
11. **Real objects over mocks.** Build collaborators; mock only true boundaries (clock, network,
    external services). Over-mocking tests the call graph, not the behaviour.

---

## 1. Why XModelBuilder changes how you test

Two classic patterns, unified: the **Object Mother** ("give me a valid Customer") and the **Test Data
Builder** ("...but with this e-mail and no addresses"). A `[ModelBuilder("name")]` class is the mother;
its `SetDefaults()` are the sensible defaults; the fluent `With` / `WithValues` are the per-test
deltas. Because the same builders are reachable from plain C#, from Gherkin tables and from the mini
data language, **your unit tests, your integration steps and your database seed all build the same
objects the same way** — one definition of "a valid Customer," not three.

The v3 layering (README ch. 5) gives you three composable tiers:

| You want… | Use |
|---|---|
| a plain, defaults-free instance | `For<T>()` (base + cross-cutting) or `ForEmpty<T>()` (base only) |
| a type's canonical shape | a specific builder via `Use<TBuilder>()` or `For<T>("name")` |
| something true of EVERY object (deterministic `Id`, tenant, audit) | the **cross-cutting layer**, `AddCrossCuttingModelBuilder(typeof(EntityDefaults<>))` |

---

## 2. Determinism — the non-negotiable foundation

Non-deterministic tests are worse than no tests. Make randomness reproducible and time controllable —
this is the **R** (Repeatable) in the classic **FIRST** properties (Fast, Isolated, Repeatable,
Self-validating, Timely) that the rest of this guide rounds out.

**Do**

- Seed the faker(s) with a fixed number: `AddXFaker(seed)`, `AddBogusFaker(seed, "nl")`. The same seed
  ⇒ the same data ⇒ the same run. (See `Support/ShopModelBuilders.cs`.)
- Use two seeds deliberately when two data sources must stay independent — the demo seeds the *seed
  data* and the *per-scenario request data* differently so they never accidentally coincide.
- Control time through `TimeProvider` (inject it; `FakeTimeProvider` in tests). Put "now"-derived
  defaults (`CreatedAt`) in the cross-cutting layer so they are consistent and overridable.
- Put a deterministic identity in the cross-cutting layer once:

  ```csharp
  public sealed class EntityDefaults<TModel>(IOptions<ModelBuilderOptions> o, IModelBuilderProvider p)
      : ModelBuilder<EntityDefaults<TModel>, TModel>(o, p) where TModel : class
  {
      protected override void SetDefaults()
      {
          // deterministic Guid, but only on types that actually have a Guid Id
          if (typeof(TModel).GetProperty("Id")?.PropertyType == typeof(Guid)) With("Id", "xfake.NewGuid()");
      }
  }
  services.AddCrossCuttingModelBuilder(typeof(EntityDefaults<>));
  ```

  The demo wires exactly this pattern (`Support/EntityDefaults.cs`), but — because its entities use
  int surrogate keys, not Guids — it stamps a deterministic audit `CreatedAt` from an injected
  (frozen) `TimeProvider` on every `IAuditable` entity, instead of a Guid `Id`. Same shape, different
  cross-cutting concern.

**Don't**

- Don't call `DateTime.Now`, `Guid.NewGuid()`, `Random.Shared` or `Environment.*` from code under test
  that a test asserts on.
- Don't depend on test execution ORDER for data. Each test arranges its own world.
- Don't assert on a value the framework randomised unless you fixed the seed and know it. Remember that
  RNG-based faker values depend on draw ORDER: adding a field upstream shifts every later random value.
  Prefer stable, name-based values (`xfake.NewGuid("customer-acme")`) or assert on a property, not on an
  exact randomised value.
- Don't leak synthetic data where it can be mistaken for real. Faker output (names, e-mails, BSN/IBAN) is
  fictitious but realistic — never seed it into shared or production-like stores, and don't treat it as
  safe to expose.

---

## 3. Unit testing with XModelBuilder

Unit tests exercise one class/method with no I/O. XModelBuilder's job here is to make the **Arrange**
step a one-liner that is both realistic and minimal.

### 3.1 Structure & conventions

- **xUnit**, one test class per unit under test, one behaviour per `[Fact]`/`[Theory]`.
- Every test body carries the `// Arrange`, `// Act`, `// Assert` markers (this repo's convention;
  combine markers for one-liners, e.g. `// Act & Assert`). Block bodies, not expression bodies.
- Name tests for the behaviour: `Method_State_ExpectedOutcome`.
- No DI needed for pure unit tests — the static facades resolve through the process-wide standalone
  provider:

  ```csharp
  var order = Create.Model<Order>();                                          // base + cross-cutting
  var vip   = Use.Builder<VipCustomerBuilder>().Build();                      // a specific mother
  var many  = Create.Models<Product>(3, (b, i) => b.With(p => p.Sku, $"SKU-{i}")); // three, varied by index
  ```

  For anything that needs registrations (fakers, specific builders), build a small provider in the
  test or a shared fixture — see README chapter 14.

### 3.2 Build the Arrange with intent

```csharp
[Fact]
public void Total_Excludes_Cancelled_Lines()
{
    // Arrange — a mother + exactly the delta that matters to THIS test
    var order = Use.Builder<OrderBuilder>()
        .With("Lines[0].Quantity", "2")
        .With("Lines[1].Status", "Cancelled")
        .Build();

    // Act
    var total = order.CalculateTotal();

    // Assert
    Assert.Equal(order.Lines[0].LineTotal, total);
}
```

The test reads as its intent: *cancelled lines don't count.* Everything not named (SKUs, prices,
addresses) came from the builder's defaults and is irrelevant noise kept out of the test.

### 3.3 Do's and don'ts (unit)

**Do**

- Put the *canonical valid shape* of a type in one `[ModelBuilder]` and reuse it everywhere.
- Override only what the test is about; let defaults carry the rest.
- Use `BuildMany(n, (b, i) => …)` for varied collections instead of copy-pasted `new`s (README ch. 12).
- Use `ForEmpty<T>()` when you specifically want a bare object (e.g. testing validation of a
  half-populated entity) — it skips the cross-cutting layer.
- Use `Extend(existing)` to layer a second dataset onto an object without re-running defaults.

**Don't**

- Don't `new` domain objects with 12 constructor arguments inline — it hides intent and breaks on
  every model change.
- Don't build a giant "god object" in a base fixture that every test silently depends on. Build per
  test; share only the *builder*, not the *instance*.
- Don't assert on incidental default values ("City == Amsterdam") unless the City is the point.
- Don't reach into `internal`/private state to arrange; if a value is hard to set, that's a design
  signal.
- Don't over-load a builder's defaults. A value that half your tests silently rely on has a wide blast
  radius when it changes — keep defaults minimal and obvious, and set what a test actually needs.

### 3.4 Test doubles: build real objects, mock only at the boundaries

XModelBuilder removes the most common reason to reach for a mock — a "valid enough" object graph — so the
question narrows to *collaborators*, not data. The rule of thumb:

- **Use the real thing (built by a builder) for values, entities and pure domain logic.** Hand-rolling a
  mock of a domain object is a smell: build it. Real objects catch real bugs that a mock's canned answers
  hide.
- **Replace only genuine boundaries with a double**: the clock (`TimeProvider`/`FakeTimeProvider`), the
  network/HTTP, the file system, message buses, payment/e-mail gateways — anything slow, external or
  nondeterministic.
- **Prefer a hand-written fake over a mocking framework** when the double has behaviour (an in-memory
  repository, a fake gateway). Reserve Moq/NSubstitute for thin, interaction checks ("was `Charge` called
  once with this amount?").
- **Don't over-mock.** A test that mocks every collaborator asserts your implementation's call graph, not
  its behaviour — it breaks on every refactor and proves little. Needing many mocks is the design telling
  you the unit does too much.
- In **integration** tests the boundary rule is identical: keep the database and app wiring real (that's
  the point), but stub truly external services — the demo swaps real auth for `TestAuthHandler`, and a
  payment provider would be stubbed the same way.

### 3.5 Parameterized tests: one behaviour, many cases

A `[Theory]` keeps "one behaviour per test" while covering a table of cases; the builder supplies the
per-case delta:

```csharp
[Theory]
[InlineData("Cancelled", 0)]
[InlineData("Shipped",   1)]
public void ActiveLineCount_CountsOnlyNonCancelledLines(string status, int expected)
{
    // Arrange
    var order = Use.Builder<OrderBuilder>().With("Lines[0].Status", status).Build();

    // Act
    var count = order.ActiveLineCount();

    // Assert
    Assert.Equal(expected, count);
}
```

Use `[InlineData]` for a handful of literals, `[MemberData]`/`[ClassData]` when a case needs a built
object or a computed value. For a *collection* that varies within one test, reach for
`BuildMany(n, (b, i) => …)` (README ch. 12) rather than a copy-pasted list.

### 3.6 Assertions: expressive, and structural for graphs

- **Standardise on one assertion style** per solution. A fluent-assertion library (Shouldly,
  AwesomeAssertions, or FluentAssertions — note FluentAssertions turned commercial at v8) reads better and
  fails clearer than a bare `Assert.Equal`; the examples here stay on plain xUnit only to avoid picking one
  for you.
- **Compare whole graphs structurally**, not field by field: `actual.Should().BeEquivalentTo(expected)`
  (or record equality) turns "assert 20 properties" into one intent-revealing line — and pairs perfectly
  with a builder that produced the `expected` deterministically.
- For large response DTOs or generated documents, **snapshot/approval testing** (Verify) often beats any
  hand-written assertion; XModelBuilder's determinism keeps the snapshots stable (see §6).

---

## 4. Integration testing with XModelBuilder (BDD / Reqnroll)

Integration tests run the real application wiring — here the ASP.NET Core API in-process via
`WebApplicationFactory<Program>` — against a real database, driven by Gherkin scenarios. This is where
structure pays off most, because the suite grows fast.

### 4.1 Layout: first by DOMAIN, then by artifact

Group everything a domain owns together, so a newcomer finds "how do I test orders?" in one folder.
Within a domain, split by artifact, **one class per file**. Shared, domain-less pieces live in
`Common/`; infrastructure in `Support/`.

```
Tests/
  Common/                       # the shared foundation (belongs to no single domain)
    Contexts/                   #   CurrentUserContext, HttpResponseContext, ApiResponse, ScenarioState (aggregate)
    Drivers/                    #   ApiDriver (generic base), AuthenticationDriver, ShopDriver (aggregate)
    Steps/                      #   CommonSteps (auth + generic asserts), RoleMap
  Domains/
    Ordering/
      Builders/                 #   [ModelBuilder("order")], [ModelBuilder("address")]
      Contexts/                 #   OrderContext
      Drivers/                  #   OrderApiDriver : ApiDriver
      Features/                 #   PlaceOrder.feature, FulfillOrder.feature
      Steps/                    #   PlaceOrderSteps, FulfillOrderSteps
    Catalog/  …
    Customers/ …                #   (a domain may have no Driver if it builds only in memory)
  Support/
    Infrastructure/             #   CustomWebApplicationFactory, ShopTestHost, TestDatabase, TestAuthHandler
    Seeding/                    #   DatabaseSeeder
    ShopModelBuilders.cs        #   the ONE XModelBuilder registration, reused by both DI layers
    ScenarioDependencies.cs     #   scenario DI composition root (Reqnroll MS DI plugin)
    DatabaseHooks.cs, HostHooks.cs
  TestParallelization.cs
```

**Why domain-first?** Feature files, the steps that bind them, the drivers they call and the builders
they use all change together. Co-locating them keeps a change to "ordering" inside `Domains/Ordering/`
and makes step re-use obvious (you look in the domain first, then in `Common/`).

### 4.2 The building blocks

**Scenario contexts** — small, mutable, scoped state carriers, ONE per domain, plus an optional
aggregate. A step writes what it produced; a later step reads it. Keep them dumb (properties + a
`Require()` guard), not logic.

```csharp
// Common/Contexts/HttpResponseContext.cs — shared "last response"
public sealed class HttpResponseContext
{
    public ApiResponse? Last { get; set; }
    public ApiResponse Require() => Last ?? throw new InvalidOperationException("No API call has been made yet.");
}
```

The optional **aggregate context** (`ScenarioState`) bundles the per-domain contexts for the rare step
that spans domains — but a single-domain step injects only its own domain's context.

**Drivers** — the "how" of talking to the system, so steps stay declarative. A **generic base** owns
the plumbing (HTTP + JSON + auth, recording the last response); **specific drivers** only express
endpoints.

```csharp
// Common/Drivers/ApiDriver.cs — plumbing once
public abstract class ApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
{
    protected Task<ApiResponse> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);
    // …attaches test-auth headers, records response.Last…
}

// Domains/Ordering/Drivers/OrderApiDriver.cs — endpoints only
public sealed class OrderApiDriver(HttpClient c, CurrentUserContext u, HttpResponseContext r) : ApiDriver(c, u, r)
{
    public Task<ApiResponse> PlaceOrder(PlaceOrderRequest request) => PostAsync("/api/orders", request);
    public Task<ApiResponse> Pay(int orderId)                       => PostAsync($"/api/orders/{orderId}/pay");
}
```

**Steps** — thin glue: build the request with XModelBuilder, call a driver, record/assert via a
context. The `PlaceOrder` step is three lines because the `"order"` builder fills the addresses and
payment, so the Gherkin table only carries the lines:

```csharp
// Domains/Ordering/Steps/PlaceOrderSteps.cs
[When(@"I place the following order:")]
public async Task WhenIPlaceTheOrder(Table table)
{
    var request = xprovider.For<PlaceOrderRequest>("order").CreateModel(table); // table -> request model
    await orders.PlaceOrder(request);
}
```

```gherkin
When I place the following order:
    | Field             | Value       |
    | Lines[0].Sku      | SKU-PHONE-1 |
    | Lines[0].Quantity | 2           |
```

**Storage helpers & seeding** — build the baseline dataset with the SAME builders you test with, so the
seed is realistic and stays in step with the model. Customers are seeded through their role-specific
named builders — the library dog-foods itself on the seed:

```csharp
// Support/Seeding/DatabaseSeeder.cs
db.Customers.Add(xprovider.For<Customer>("customer").With(c => c.Email, "alice@shop.test")…Build());
db.Customers.Add(xprovider.For<Customer>("admin").With(c => c.Email, "admin@shop.test")…Build());
```

### 4.3 Isolation: one expensive setup, cheap per-scenario reset

The pattern that keeps a database-backed suite both realistic and fast:

1. **Once per run** (`[BeforeTestRun]`, `HostHooks` → `ShopTestHost`): create the schema, open ONE
   shared `SqlConnection`, build the `WebApplicationFactory`, and **commit** the seed as the baseline.
2. **Once per scenario** (`[BeforeScenario]`/`[AfterScenario]`, `DatabaseHooks`): begin a transaction
   on the shared connection, run the scenario, then **roll it back** — resetting the store to the
   committed seed without touching the schema.

```csharp
// Support/DatabaseHooks.cs
[BeforeScenario(Order = 0)] public void Begin()    => HostHooks.Instance.Database.BeginScenarioTransaction();
[AfterScenario(Order = 0)]  public void Rollback() => HostHooks.Instance.Database.RollbackScenarioTransaction();
```

Key infrastructure choices (`Support/Infrastructure/TestDatabase.cs`):

- **One physical connection** shared by test code AND the in-process API, so a single transaction wraps
  both — and no second connection ever enlists, so the transaction is never promoted to MSDTC (which
  LocalDB doesn't support).
- Because the seed is committed but the scenario's writes are not, you can inspect them in SSMS via
  `READ UNCOMMITTED` while paused at a breakpoint.
- **Scenarios run sequentially** — the shared connection is not thread-safe
  (`TestParallelization.cs` disables xUnit parallelization). Parallelism, if you need it, goes at a
  higher grain: shard by *assembly* / test project, or give each worker its own database.

### 4.4 The three DI layers (keep them straight)

1. **Application DI** — `Program.cs`. The real thing; don't fork it in tests.
2. **Test-base DI** — `CustomWebApplicationFactory.ConfigureTestServices`: rewire `DbContext` onto the
   shared test connection, swap real auth for a header-based `TestAuthHandler`, register XModelBuilder +
   seeded fakers for the seeder.
3. **Scenario DI** — `ScenarioDependencies` (Reqnroll's MS DI plugin): a fresh container per scenario
   registering the per-domain contexts and drivers (`AddScoped`), plus its own XModelBuilder provider
   (a different seed) for building request models in steps.

XModelBuilder is registered once in `ShopModelBuilders.AddShopModelBuilders(seed)` and reused by both
test-base and scenario layers, so "how a Customer is built" is defined in exactly one place.

> **Isolation knob.** If you run scenarios with a scope-per-scenario and want each scope to get its own
> provider + fakers + seeded RNGs, register with `AddXModelBuilder(isolation:
> XModelBuilderIsolation.PerScope)` (README ch. 21.1). The demo uses `Shared` because its reset is the
> DB transaction, not the container.

### 4.5 Gherkin ↔ XModelBuilder

- **Vertical `Field | Value` table → one model:** `xprovider.For<T>("name").CreateModel(table)`.
- **Horizontal table → one model per row:** `xprovider.CreateModels<T>(table)` (or `(table, "name")`).
- **Deep paths in cells** (`Lines[0].Sku`, `Address.City`) let one compact table describe a graph.
- **Named-builder references** resolve automatically for reference-typed members in a cell, so a table
  can say `order` and get the `"order"` builder.
- **Compose across steps with `Extend`:** build a Customer as a person in one step, then add a shipping
  and a billing address in later steps via `Extend` — without re-running the builder's defaults
  (README ch. 18; `Domains/Customers`).

### 4.6 Do's and don'ts (integration)

**Do**

- Keep steps declarative; push all mechanics into drivers and all state into contexts.
- Reuse `Common/` steps for cross-cutting concerns (auth, "I am rejected as forbidden") instead of
  re-implementing per feature.
- Assert through the public surface (HTTP status + response DTO). Only read the DB directly to verify a
  side effect the API doesn't expose.
- Make the seed the *minimum* believable world; let scenarios add what they need on top.
- Write negative scenarios (401/403/409) next to the happy path — they're the cheapest bugs to catch.

**Don't**

- Don't share mutable instances between scenarios; the transaction reset only covers the database.
- Don't put assertions in drivers or business logic in steps.
- Don't let one scenario depend on another having run first.
- Don't reach past the transaction (e.g. a background service on its own connection) and then wonder
  why rollback "didn't work."
- Don't grow one `CommonSteps` god-class; promote to `Common/` only what is genuinely shared, keep the
  rest in the owning domain.

---

## 5. Bootstrapping the suites for a new large project

A day-one checklist that scales from the first feature to hundreds:

1. **Create two test projects**: `<Product>.UnitTests` and `<Product>.IntegrationTests` (xUnit,
   net10.0, `Nullable`/`ImplicitUsings` on).
2. **Add XModelBuilder + a faker** and one `AddXModelBuilder` registration module reused everywhere
   (`XModelBuilders.cs`). Register the **cross-cutting layer** immediately with your identity/audit
   defaults — do it before anyone writes a builder, so it's never forgotten.
3. **Establish the folder convention** (§4.1) empty-but-present: `Common/{Contexts,Drivers,Steps}`,
   `Domains/`, `Support/{Infrastructure,Seeding}`. Convention beats a wiki page.
4. **Stand up the integration host once**: `WebApplicationFactory`, a `TestDatabase` (shared
   connection), `HostHooks`/`DatabaseHooks` for the run/scenario lifecycle, a `TestAuthHandler` for
   header-based auth, and a `DatabaseSeeder` that builds the baseline with your builders.
5. **Pick your database strategy** (§6) and wire the reset (transaction rollback, or per-worker DB).
6. **Write the first vertical slice end to end** for one domain — feature → steps → driver → builder →
   context — and treat it as the template every later domain copies.
7. **Turn on CI discipline** from commit #1: fast unit tests always; DB-bound integration tests behind
   a container or on a self-hosted agent; zero tolerance for flakes.

The point of the framework here is that steps 2, 6 and the seed all share ONE definition of your test
data, so the suite starts consistent and stays consistent as the model grows.

---

## 6. Modern practices & newer insights

- **Prefer in-process over out-of-process.** `WebApplicationFactory<Program>` gives you real routing,
  auth, filters and DI at ~unit-test speed, and lets one transaction wrap test + server. Reserve full
  external stacks for a thin smoke suite.
- **Database choice is a spectrum.** LocalDB (as in the demo) is zero-infra on Windows and SSMS-
  inspectable. **Testcontainers** (SQL Server / Postgres in Docker) is the portable, CI-friendly choice
  and enables per-worker parallelism (a DB per container). An in-memory provider is fine only for logic
  that doesn't touch provider-specific SQL — it lies about relational behaviour, so don't trust it for
  integration coverage.
- **Control the clock.** Inject `TimeProvider` everywhere; use `FakeTimeProvider` in tests and drive
  time-dependent defaults through the cross-cutting layer. Time-travel scenarios then become data, not
  `Thread.Sleep`.
- **Respect the test pyramid, but invest in the "trophy" middle.** Many fast unit tests, a strong band
  of in-process integration tests (highest bug-per-minute), few true end-to-end tests.
- **Assert behaviour, and consider approval/snapshot testing** (e.g. Verify) for large response DTOs or
  generated documents — it turns "assert 30 fields" into one reviewed snapshot. Combine with
  XModelBuilder's determinism so snapshots are stable.
- **Parallelism is a data-isolation decision, not a switch.** Only parallelise when each worker owns its
  data (separate DB/schema/tenant). The demo trades parallelism for a shared-connection transaction
  reset; a Testcontainers-per-worker setup trades setup cost for parallelism. Choose deliberately.
- **Flaky-test hygiene.** No `[Retry]` as a cure. Quarantine, reproduce with a fixed seed, fix the root
  cause (usually shared state, real time, or ordering). Track flake rate as a quality metric.
- **Contract-first request builders.** Build the API's request DTOs with XModelBuilder (see the
  `"order"`/`PlaceOrderRequest` builder), not the domain entities, for API tests — you then test the
  contract your clients actually use.
- **Coverage is a diagnostic, not a target.** High line coverage of shallow assertions proves nothing;
  chase behaviour. To measure whether your assertions actually catch regressions, prefer mutation testing
  (Stryker.NET) over a coverage percentage as a quality gate.
- **Categorise and separate suites so CI can run them independently.** Use separate projects (unit vs
  integration) and/or xUnit `[Trait("Category","Integration")]` filtered with `dotnet test --filter`. Fast
  tests gate every push; slow DB-bound tests run where the infrastructure exists.
- **Mind async hygiene.** Async tests are `async Task`, never `async void` (an `async void` test cannot be
  awaited and its failures are silently lost); never block on `.Result`/`.Wait()` in tests — it deadlocks
  and buries the real error. Await the driver, as the step examples do.
- **Keep test code to production standards.** One class per file, XML-doc the shared foundation
  (drivers, contexts, infrastructure), review it like shipping code. The suite is a product; a big
  project lives or dies by how maintainable its tests are.

---

## 7. Where to look next

- [`Demo/README.md`](../Demo/README.md) — the full worked example this guide references.
- README ch. 5 (base + cross-cutting layer + named builders), ch. 12 (`BuildMany`),
  ch. 14 (standalone facades), ch. 18 (Reqnroll/SpecFlow + `Extend`), ch. 21.1 (isolation).
- [`docs/scenarios/`](scenarios/) — focused, runnable data-shaping scenarios (time-travel, related
  graphs, a deterministic Dutch dataset).
- [`docs/adr/`](adr/) — the design decisions behind the resolution model.
