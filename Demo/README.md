# XModelBuilder Demo — Webshop + Reqnroll-integratietests

Een realistische demo die laat zien hoe XModelBuilder testdata bouwt in end-to-end
integratietests, volgens de-facto standaarden voor DI, drivers, scenariocontexts en
een (test-)database.

## Projecten

- **`XModelBuilder.Demo.Shop`** — een ASP.NET Core Web API (.NET 10, EF Core, SQL Server).
  Bevat één niet-triviale object-graph: `Customer → Address[]/PaymentMethod[]`,
  `Order → OrderLine[] → Product → Category` (self-referencing), plus owned
  `OrderAddress`, `Payment` en `OrderStatusHistory[]`. Rol-gebaseerde autorisatie
  (`Guest`/`Customer`/`WarehouseOperator`/`Admin`). `Program.cs` is de **applicatie-DI**.
- **`XModelBuilder.Demo.Shop.IntegrationTests`** — Reqnroll-tests die de API in-process
  draaien via `WebApplicationFactory<Program>`.

## Drie features, meerdere scenario's, meerdere rollen

- `Features/BestellingPlaatsen.feature` — geldige bestelling, onvoldoende voorraad,
  gast (401), kortingscode.
- `Features/BestellingUitleveren.feature` — magazijn verzendt betaalde order, onbetaalde
  order geweigerd (409), klant mag niet verzenden (403).
- `Features/CatalogusEnToegang.feature` — beheerder voegt product toe, klant verboden
  (403), klant ziet andermans orders niet (403).

## Indeling (generiek ↔ specifiek)

- `Drivers/` — generieke `ApiDriver` (HTTP+JSON+auth) → specifieke `OrderApiDriver`,
  `CatalogApiDriver`, `AuthenticationDriver` → geaggregeerde `ShopDriver`. Alles via
  constructor-injectie.
- `Contexts/` — getypeerde, per-scenario contexts (`CurrentUserContext`,
  `HttpResponseContext`, `OrderContext`, `CatalogContext`) + geaggregeerde `ScenarioState`.
- `ModelBuilders/` — XModelBuilder builders; named per rol (`[ModelBuilder("customer"|
  "warehouse"|"admin")]`) en een `"order"`-builder die de request-graph met defaults vult.
  `ShopModelBuilders.AddShopModelBuilders(seed)` wordt door **beide** DI-lagen hergebruikt.
- `Support/Infrastructure/` — `CustomWebApplicationFactory` (test-basis-DI),
  `TestDatabase` (gedeelde connectie + transactie), `TestAuthHandler`.
- `Support/Seeding/` — `DatabaseSeeder` bouwt de initiële dataset met XModelBuilder.
- `Support/ScenarioDependencies.cs` — de **scenario-specifieke DI** (Reqnroll MS DI-plugin).

## Drie DI-lagen

1. **Applicatie** — `Program.cs` (EF Core + SQL Server, auth, services).
2. **Test-basis** — `CustomWebApplicationFactory.ConfigureTestServices`: wisselt de
   `ShopDbContext` naar de gedeelde testconnectie, vervangt auth door `TestAuthHandler`,
   registreert XModelBuilder + geseede XFaker (voor de seeder).
3. **Scenario-specifiek** — `ScenarioDependencies` + hooks: per-scenario contexts, drivers
   en een eigen XModelBuilder-provider (andere seed) voor het bouwen van requests in steps.

## Storage & reset (performance)

- **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`, database `XModelBuilderDemoTests`),
  zodat je de data tijdens debuggen in SSMS/Azure Data Studio kunt inzien.
- Schema wordt 1× opgebouwd, de **seed 1× gecommit** als baseline.
- **Per scenario** loopt alles door **één gedeelde `SqlConnection`** binnen een transactie
  die na afloop wordt teruggedraaid (`[BeforeScenario]`/`[AfterScenario]`), zodat de store
  terug is op de seed. Één fysieke connectie ⇒ geen MSDTC-promotie op LocalDB.
- Scenario's draaien **sequentieel** (xUnit-parallelisatie staat uit): de gedeelde
  connectie is niet thread-safe.

## Draaien

```powershell
dotnet test Demo/XModelBuilder.Demo.Shop.IntegrationTests
```

Vereist een lokale **SQL Server LocalDB** (`sqllocaldb info` toont `MSSQLLocalDB`).
