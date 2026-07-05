# XModelBuilder Demo — Web Shop + Reqnroll integration tests

A realistic demo showing how XModelBuilder builds test data in end-to-end integration
tests, following de-facto standards for DI, drivers, scenario contexts and a (test)
database.

## Projects

- **`XModelBuilder.Demo.Shop`** — an ASP.NET Core Web API (.NET 10, EF Core, SQL Server).
  It contains one non-trivial object graph: `Customer → Address[]/PaymentMethod[]`,
  `Order → OrderLine[] → Product → Category` (self-referencing), plus an owned
  `OrderAddress`, a `Payment` and `OrderStatusHistory[]`. Role-based authorization
  (`Guest`/`Customer`/`WarehouseOperator`/`Admin`). `Program.cs` is the **application DI**.
- **`XModelBuilder.Demo.Shop.IntegrationTests`** — Reqnroll tests that run the API
  in-process via `WebApplicationFactory<Program>`.

## Four features, multiple scenarios, multiple roles

- `Ordering/BestellingPlaatsen.feature` — a valid order, insufficient stock, guest (401),
  a discount code.
- `Ordering/BestellingUitleveren.feature` — the warehouse ships a paid order, an unpaid
  order is rejected (409), a customer may not ship (403).
- `Catalog/CatalogusEnToegang.feature` — an admin adds a product, a customer is forbidden
  (403), a customer cannot see another customer's orders (403).
- `Customers/KlantSamenstellen.feature` — a customer is **built up in aggregate**: first as
  a person, then **extended** with a shipping and billing address in separate Gherkin steps
  via XModelBuilder's `Extend` (without re-running the builder defaults).

## Layout by domain (one class per file)

The tests are grouped per domain; each domain has its own driver, model builders, steps,
feature(s) and **its own scenario context**. Every class lives in its own file.

- `Common/` — the shared foundation: the generic `ApiDriver` (HTTP+JSON+auth),
  `AuthenticationDriver`, `ApiResponse`, `CurrentUserContext`, `HttpResponseContext`,
  `CommonSteps` (auth + generic authorization asserts) and `RoleMap`.
- `Ordering/` — `OrderApiDriver`, `OrderContext`, the `"order"`/`"address"` builders and the
  `PlaceOrderSteps`/`FulfillOrderSteps`.
- `Catalog/` — `CatalogApiDriver`, `CatalogContext`, `ProductBuilder`, `CatalogAccessSteps`.
- `Customers/` — the role-based `Customer` builders (`[ModelBuilder("customer"|"warehouse"|
  "admin")]`), the `"customerAddress"` builder, `CustomerBuildContext` and the
  `CustomerBuildSteps` of the aggregated build.
- `Aggregate/` — the **optional** aggregate `ShopDriver` + `ScenarioState`, bundling the
  per-domain drivers/contexts for steps that combine several domains.
- `Support/Infrastructure/` — `CustomWebApplicationFactory` (test-base DI),
  `TestDatabase` (shared connection + transaction), `TestAuthHandler`.
- `Support/Seeding/` — `DatabaseSeeder` builds the initial dataset with XModelBuilder.
- `Support/ShopModelBuilders.cs` — registers the provider, **both fakers** (XFaker + Bogus)
  and all builders; reused by **both** DI layers.
- `Support/ScenarioDependencies.cs` — the **scenario-specific DI** (Reqnroll MS DI plugin).

## Two fakers side by side (XFaker + Bogus)

Both fakers are registered at once to show they coexist without colliding (their tokens are
namespaced): **XFaker** produces deterministic product SKUs, while **Bogus** (locale `"nl"`)
generates realistic Dutch names, e-mail addresses and addresses for customers and addresses.

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
