# XModelBuilder Demo — Web Shop + Reqnroll integration tests

A realistic demo showing how XModelBuilder builds test data in end-to-end integration
tests, following de-facto standards for DI, drivers, scenario contexts and a (test)
database.

## Projects

- **`XModelBuilder.Demo.Shop`** — an ASP.NET Core Web API (.NET 8, EF Core, SQL Server).
  It contains one non-trivial object graph: `Customer → Address[]/PaymentMethod[]`,
  `Order → OrderLine[] → Product → Category` (self-referencing), plus an owned
  `OrderAddress`, a `Payment` and `OrderStatusHistory[]`. Role-based authorization
  (`Guest`/`Customer`/`WarehouseOperator`/`Admin`). `Program.cs` is the **application DI**.
- **`XModelBuilder.Demo.Shop.IntegrationTests`** — Reqnroll tests that run the API
  in-process via `WebApplicationFactory<Program>`.

## Three features, multiple scenarios, multiple roles

- `Features/BestellingPlaatsen.feature` — a valid order, insufficient stock, guest (401),
  a discount code.
- `Features/BestellingUitleveren.feature` — the warehouse ships a paid order, an unpaid
  order is rejected (409), a customer may not ship (403).
- `Features/CatalogusEnToegang.feature` — an admin adds a product, a customer is forbidden
  (403), a customer cannot see another customer's orders (403).

## Layout (generic ↔ specific)

- `Drivers/` — a generic `ApiDriver` (HTTP+JSON+auth) → specific `OrderApiDriver`,
  `CatalogApiDriver`, `AuthenticationDriver` → an aggregate `ShopDriver`. Everything is
  constructor-injected.
- `Contexts/` — typed, per-scenario contexts (`CurrentUserContext`, `HttpResponseContext`,
  `OrderContext`, `CatalogContext`) plus an aggregate `ScenarioState`.
- `ModelBuilders/` — XModelBuilder builders; named per role (`[ModelBuilder("customer"|
  "warehouse"|"admin")]`) and an `"order"` builder that fills the request graph with
  defaults. `ShopModelBuilders.AddShopModelBuilders(seed)` is reused by **both** DI layers.
- `Support/Infrastructure/` — `CustomWebApplicationFactory` (test-base DI),
  `TestDatabase` (shared connection + transaction), `TestAuthHandler`.
- `Support/Seeding/` — `DatabaseSeeder` builds the initial dataset with XModelBuilder.
- `Support/ScenarioDependencies.cs` — the **scenario-specific DI** (Reqnroll MS DI plugin).

## Three DI layers

1. **Application** — `Program.cs` (EF Core + SQL Server, auth, services).
2. **Test base** — `CustomWebApplicationFactory.ConfigureTestServices`: rewires the
   `ShopDbContext` onto the shared test connection, replaces auth with `TestAuthHandler`,
   and registers XModelBuilder + a seeded XFaker (for the seeder).
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
