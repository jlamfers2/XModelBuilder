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

## Vier features, meerdere scenario's, meerdere rollen

- `Domains/Ordering/Features/BestellingPlaatsen.feature` — geldige bestelling, onvoldoende
  voorraad, gast (401), kortingscode.
- `Domains/Ordering/Features/BestellingUitleveren.feature` — magazijn verzendt betaalde order,
  onbetaalde order geweigerd (409), klant mag niet verzenden (403).
- `Domains/Catalog/Features/CatalogusEnToegang.feature` — beheerder voegt product toe, klant
  verboden (403), klant ziet andermans orders niet (403).
- `Domains/Customers/Features/KlantSamenstellen.feature` — een klant wordt **geaggregeerd opgebouwd**:
  eerst als persoon, daarna in aparte Gherkin-regels **uitgebreid** met een verzend- en
  factuuradres via XModelBuilders `Extend` (zonder de builder-defaults opnieuw te draaien).

## Indeling: eerst per domein, dan per artefact (één class per bestand)

De tests zijn getrapt geordend: onder `Domains/` staat elk domein (Customers, Catalog,
Ordering) met daaronder — voor zover van toepassing — vaste artefactmappen `Features`,
`Drivers`, `Steps`, `Builders` en `Contexts`. `Common/` volgt dezelfde artefactindeling
voor het gedeelde fundament. Elke class staat in een eigen bestand; elk domein heeft zijn
**eigen scenariocontext**.

```
Domains/
  Customers/   Features · Steps · Builders · Contexts        (geen Drivers: bouwt modellen in-memory)
  Catalog/     Features · Drivers · Steps · Builders · Contexts
  Ordering/    Features · Drivers · Steps · Builders · Contexts
Common/        Drivers · Steps · Contexts
Support/       Infrastructure · Seeding · DI-registratie
```

- `Common/` — het gedeelde fundament: `Drivers/` (generieke `ApiDriver` (HTTP+JSON+auth),
  `AuthenticationDriver` en de optionele aggregatie-`ShopDriver`), `Steps/` (`CommonSteps`
  met auth + generieke autorisatie-asserts, en `RoleMap`) en `Contexts/`
  (`CurrentUserContext`, `HttpResponseContext`, `ApiResponse` en de optionele aggregatie-
  `ScenarioState`).
- `Domains/Ordering/` — `Drivers/OrderApiDriver`, `Contexts/OrderContext`,
  `Builders/` (`"order"` + `"address"`) en `Steps/` (`PlaceOrderSteps`, `FulfillOrderSteps`).
- `Domains/Catalog/` — `CatalogApiDriver`, `CatalogContext`, `ProductBuilder`, `CatalogAccessSteps`.
- `Domains/Customers/` — de rol-gebaseerde `Customer`-builders (`[ModelBuilder("customer"|
  "warehouse"|"admin")]`) + de `"customerAddress"`-builder, `CustomerBuildContext` en de
  `CustomerBuildSteps` van de geaggregeerde opbouw (geen eigen driver).
- `Support/Infrastructure/` — `CustomWebApplicationFactory` (test-basis-DI),
  `TestDatabase` (gedeelde connectie + transactie), `TestAuthHandler`.
- `Support/Seeding/` — `DatabaseSeeder` bouwt de initiële dataset met XModelBuilder.
- `Support/ShopModelBuilders.cs` — registreert de provider, **beide fakers** (XFaker +
  Bogus) en alle builders; door **beide** DI-lagen hergebruikt.
- `Support/ScenarioDependencies.cs` — de **scenario-specifieke DI** (Reqnroll MS DI-plugin).

De aggregatie-`ShopDriver`/`ScenarioState` bundelen de per-domein drivers/contexts voor
stappen die meerdere domeinen combineren; ze wonen in `Common/` omdat ze niet bij één domein horen.

## Twee fakers naast elkaar (XFaker + Bogus)

Beide fakers zijn tegelijk geregistreerd om te tonen dat ze samengaan zonder te botsen
(hun tokens zijn genamespaced): **XFaker** levert deterministische product-SKU's, terwijl
**Bogus** (locale `"nl"`) realistische Nederlandse namen, e-mailadressen en adressen genereert
voor klanten en adressen.

## Drie DI-lagen

1. **Applicatie** — `Program.cs` (EF Core + SQL Server, auth, services).
2. **Test-basis** — `CustomWebApplicationFactory.ConfigureTestServices`: wisselt de
   `ShopDbContext` naar de gedeelde testconnectie, vervangt auth door `TestAuthHandler`,
   registreert XModelBuilder + geseede XFaker en Bogus (voor de seeder).
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
