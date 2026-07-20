# XModelBuilder Demo — Web Shop + Reqnroll integration tests

A realistic demo showing how XModelBuilder builds test data in end-to-end integration
tests, following de-facto standards for DI, drivers, scenario contexts and a (test)
database.

> **Setting up tests for your own project?** This demo is the worked example behind
> [`docs/testing-best-practices.md`](../docs/testing-best-practices.md) — a guide to structuring unit
> and integration suites (domain-grouped steps, drivers, contexts, storage helpers, isolated DB
> transactions, do's & don'ts) for a large professional project. Read that alongside this README.

## Projects

- **`XModelBuilder.Demo.Shop`** — an ASP.NET Core Web API (.NET 10, EF Core, SQL Server).
  It contains one non-trivial object graph: `Customer → Address[]/PaymentMethod[]`,
  `Order → OrderLine[] → Product → Category` (self-referencing), plus an owned
  `OrderAddress`, a `Payment` and `OrderStatusHistory[]`. Role-based authorization
  (`Guest`/`Customer`/`WarehouseOperator`/`Admin`). `Program.cs` is the **application DI**.
- **`XModelBuilder.Demo.Shop.IntegrationTests`** — Reqnroll tests that run the API
  in-process via `WebApplicationFactory<Program>`.

## Four features, multiple scenarios, multiple roles

- `Domains/Ordering/Features/PlaceOrder.feature` — a valid order, insufficient stock,
  guest (401), a discount code.
- `Domains/Ordering/Features/FulfillOrder.feature` — the warehouse ships a paid order,
  an unpaid order is rejected (409), a customer may not ship (403).
- `Domains/Catalog/Features/CatalogAndAccess.feature` — an admin adds a product, a customer
  is forbidden (403), a customer cannot see another customer's orders (403).
- `Domains/Customers/Features/BuildCustomer.feature` — a customer is **built up in aggregate**:
  first as a person, then **extended** with a shipping and billing address in separate Gherkin
  steps via XModelBuilder's `Extend` (without re-running the builder defaults).

## Layout: first by domain, then by artifact (one class per file)

The tests are organised in two tiers: under `Domains/` each domain (Customers, Catalog,
Ordering) holds fixed artifact folders — where applicable — `Features`, `Drivers`, `Steps`,
`Builders` and `Contexts`. `Common/` follows the same artifact split for the shared
foundation. Every class lives in its own file; each domain has its **own scenario context**.

```
Domains/
  Customers/   Features · Steps · Builders · Contexts        (no Drivers: it builds models in-memory)
  Catalog/     Features · Drivers · Steps · Builders · Contexts
  Ordering/    Features · Drivers · Steps · Builders · Contexts
Common/        Drivers · Steps · Contexts
Support/       Infrastructure · Seeding · DI registration
```

- `Common/` — the shared foundation: `Drivers/` (generic `ApiDriver` (HTTP+JSON+auth),
  `AuthenticationDriver` and the optional aggregate `ShopDriver`), `Steps/` (`CommonSteps`
  with auth + generic authorization asserts, and `RoleMap`) and `Contexts/`
  (`CurrentUserContext`, `HttpResponseContext`, `ApiResponse` and the optional aggregate
  `ScenarioState`).
- `Domains/Ordering/` — `Drivers/OrderApiDriver`, `Contexts/OrderContext`,
  `Builders/` (`"order"` + `"address"`) and `Steps/` (`PlaceOrderSteps`, `FulfillOrderSteps`).
- `Domains/Catalog/` — `CatalogApiDriver`, `CatalogContext`, `ProductBuilder`, `CatalogAccessSteps`.
- `Domains/Customers/` — the role-based `Customer` builders (`[ModelBuilder("customer"|
  "warehouse"|"admin")]`) + the `"customerAddress"` builder, `CustomerBuildContext` and the
  `CustomerBuildSteps` of the aggregated build (no driver of its own).
- `Support/Infrastructure/` — `CustomWebApplicationFactory` (test-base DI),
  `TestDatabase` (shared connection + transaction), `TestAuthHandler`.
- `Support/Seeding/` — `DatabaseSeeder` builds the initial dataset with XModelBuilder.
- `Support/ShopModelBuilders.cs` — registers the provider, **both fakers** (XFaker + Bogus),
  the **cross-cutting layer** (`EntityDefaults<>`) and all builders; reused by **both** DI layers.
- `Support/EntityDefaults.cs` — the cross-cutting layer; `Support/FixedTimeProvider.cs` — the frozen clock.
- `Support/ScenarioDependencies.cs` — the **scenario-specific DI** (Reqnroll MS DI plugin).

The aggregate `ShopDriver`/`ScenarioState` bundle the per-domain drivers/contexts for steps
that combine several domains; they live in `Common/` because they belong to no single domain.

## Two fakers side by side (XFaker + Bogus)

Both fakers are registered at once to show they coexist without colliding (their tokens are
namespaced): **XFaker** produces deterministic product SKUs, while **Bogus** (locale `"nl"`)
generates realistic Dutch names, e-mail addresses and addresses for customers and addresses.

## The cross-cutting layer (audit `CreatedAt`)

`ShopModelBuilders` also registers a **cross-cutting layer** —
`AddCrossCuttingModelBuilder(typeof(EntityDefaults<>))` (README chapter 5). It runs on **every** build
and stamps a deterministic `CreatedAt` on every entity that implements `IAuditable`
(`Customer`, `Product`, `Category`) — a concern true of *every* object, defined **once** instead of in
each builder. The timestamp comes from a frozen `FixedTimeProvider` (a dependency-free stand-in for
`FakeTimeProvider`) that replaces the app's system clock in tests, so `CreatedAt`, the server's order
timestamps and XFaker's age tokens are all reproducible. A specific builder or an explicit `With`
still overrides the stamp, and `ForEmpty<T>()` opts out — as the seeder does when it builds bare
`Address` value objects.

## Three DI layers

1. **Application** — `Program.cs` (EF Core + SQL Server, auth, services).
2. **Test base** — `CustomWebApplicationFactory.ConfigureTestServices`: rewires the
   `ShopDbContext` onto the shared test connection, replaces auth with `TestAuthHandler`,
   and registers XModelBuilder + seeded XFaker and Bogus (for the seeder).
3. **Scenario-specific** — `ScenarioDependencies` + hooks: per-scenario contexts, drivers
   and an XModelBuilder provider of their own (a different seed) for building requests in steps.

## Storage & reset (performance)

- **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`, database `XModelBuilderDemoTests`), so
  you can inspect the data in SSMS/Azure Data Studio while debugging.
- The schema is created once and the **seed is committed once** as the baseline.
- **Per scenario** everything runs through **one shared `SqlConnection`** inside a
  transaction that is rolled back afterwards (`[BeforeScenario]`/`[AfterScenario]`), so the
  store is reset to the seed. A single physical connection means no MSDTC promotion on LocalDB.
- Scenarios run **sequentially** (xUnit parallelization is disabled): the shared connection
  is not thread-safe.

## Running

```powershell
dotnet test Demo/XModelBuilder.Demo.Shop.IntegrationTests
```

Requires a local **SQL Server LocalDB** (`sqllocaldb info` shows `MSSQLLocalDB`).
