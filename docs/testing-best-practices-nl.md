# Testing best practices met XModelBuilder

*(English version: [`testing-best-practices.md`](testing-best-practices.md).)*

Een praktische, uitgesproken gids voor het opzetten van **unit-tests** en **integratietests** voor een
groot, professioneel project, met XModelBuilder als de enige bron van deterministische testdata. Hij
behandelt hoe je de suites structureert, do's en don'ts, en de concrete bouwstenen — domein-gegroepeerde
step-definitions, drivers, scenario-contexts, storage-helpers, geïsoleerde database-transacties — plus de
moderne praktijken die een grote suite snel en onderhoudbaar houden.

De uitgewerkte referentie in dit document is de demo-webshop onder
[`Demo/XModelBuilder.Demo.Shop.IntegrationTests`](../Demo/README-nl.md); bestandsnamen in code-commentaar
hieronder verwijzen naar echte bestanden daar. Lees dit document samen met README hoofdstuk 5
(builder-resolutie), hoofdstuk 12 (`BuildMany`), hoofdstuk 18 (Reqnroll/SpecFlow + `Extend`) en
hoofdstuk 21.1 (isolatie).

---

## 0. TL;DR — de elf regels

1. **Testdata heeft ÉÉN eigenaar: een builder.** Nooit een domeinobject met de hand `new()`-en in een
   test; bouw het.
2. **Deterministisch by default.** Vaste seed, vaste klok (`TimeProvider`); geen `DateTime.Now` of
   `Guid.NewGuid()` in code waarop je assert — vervang die door deterministische defaults (een seeded
   faker, en een Guid/audit-stempel in de cross-cutting laag).
3. **`Use<TBuilder>()` / `For<T>("name")` voor specifieke builders; `For<T>()` voor de kale basis;
   `ForEmpty<T>()` om cross-cutting defaults over te slaan.** Weet welke je bedoelt (README hfst. 5).
4. **Eén gedrag per test.** Arrange-Act-Assert, of Given-When-Then. Geen "en ook nog"-tests.
5. **Structureer eerst per DOMEIN, dan per artefact.** Vindbaarheid wint van slimmigheid.
6. **Steps zijn dun; drivers doen het werk; contexts dragen de state.** Een step is een zin, geen
   programma.
7. **Isoleer elke test.** Unit-tests delen niets; integratiescenario's resetten naar een gecommitte seed
   via een teruggedraaide transactie.
8. **Assert gedrag, geen implementatie.** Verkies de publieke API / HTTP-oppervlakte boven het porren in
   de DB.
9. **Snelle feedback wint.** In-process host, één dure setup per run, goedkope reset per scenario.
10. **Bestrijd flakiness meteen.** Een flaky test is een kapotte test; quarantaine en fix, nooit
    `[Retry]`.
11. **Echte objecten boven mocks.** Bouw collaborators; mock alleen echte grenzen (klok, netwerk,
    externe services). Over-mocken test de call-graph, niet het gedrag.

---

## 1. Waarom XModelBuilder verandert hoe je test

Twee klassieke patronen, verenigd: de **Object Mother** ("geef me een geldige Customer") en de **Test Data
Builder** ("...maar met dit e-mailadres en zonder adressen"). Een `[ModelBuilder("name")]`-klasse is de
mother; haar `SetDefaults()` zijn de zinvolle defaults; de fluent `With` / `WithValues` zijn de per-test
delta's. Omdat dezelfde builders bereikbaar zijn vanuit gewoon C#, vanuit Gherkin-tabellen en vanuit de
mini-datataal, **bouwen je unit-tests, je integratie-steps en je database-seed allemaal dezelfde objecten
op dezelfde manier** — één definitie van "een geldige Customer", niet drie.

De v3-gelaagdheid (README hfst. 5) geeft je drie samenstelbare niveaus:

| Je wilt… | Gebruik |
|---|---|
| een kale instance zonder defaults | `For<T>()` (basis + cross-cutting) of `ForEmpty<T>()` (alleen basis) |
| de canonieke vorm van een type | een specifieke builder via `Use<TBuilder>()` of `For<T>("name")` |
| iets wat voor ELK object geldt (deterministische `Id`, tenant, audit) | de **cross-cutting laag**, `AddCrossCuttingModelBuilder(typeof(EntityDefaults<>))` |

---

## 2. Determinisme — het niet-onderhandelbare fundament

Niet-deterministische tests zijn erger dan geen tests. Maak willekeur reproduceerbaar en tijd
beheersbaar — dit is de **R** (Repeatable) uit de klassieke **FIRST**-eigenschappen (Fast, Isolated,
Repeatable, Self-validating, Timely) die de rest van deze gids invult.

**Wel**

- Seed de faker(s) met een vast getal: `AddXFaker(seed)`, `AddBogusFaker(seed, "nl")`. Dezelfde seed
  ⇒ dezelfde data ⇒ dezelfde run. (Zie `Support/ShopModelBuilders.cs`.)
- Gebruik bewust twee seeds als twee databronnen onafhankelijk moeten blijven — de demo seedt de
  *seed-data* en de *per-scenario request-data* verschillend zodat ze nooit per ongeluk samenvallen.
- Beheer tijd via `TimeProvider` (injecteer hem; `FakeTimeProvider` in tests). Zet "nu"-afgeleide
  defaults (`CreatedAt`) in de cross-cutting laag zodat ze consistent én overschrijfbaar zijn.
- Zet een deterministische identiteit één keer in de cross-cutting laag:

  ```csharp
  public sealed class EntityDefaults<TModel>(IOptions<ModelBuilderOptions> o, IModelBuilderProvider p)
      : ModelBuilder<EntityDefaults<TModel>, TModel>(o, p) where TModel : class
  {
      protected override void SetDefaults()
      {
          // deterministische Guid, maar alleen op types die echt een Guid Id hebben
          if (typeof(TModel).GetProperty("Id")?.PropertyType == typeof(Guid)) With("Id", "xfake.NewGuid()");
      }
  }
  services.AddCrossCuttingModelBuilder(typeof(EntityDefaults<>));
  ```

  De demo bedraadt precies dit patroon (`Support/EntityDefaults.cs`), maar — omdat zijn entiteiten int
  surrogate-keys gebruiken, geen Guids — stempelt hij een deterministische audit-`CreatedAt` vanuit een
  geïnjecteerde (bevroren) `TimeProvider` op elke `IAuditable`-entiteit, in plaats van een Guid `Id`.
  Dezelfde vorm, een andere cross-cutting concern.

**Niet**

- Roep geen `DateTime.Now`, `Guid.NewGuid()`, `Random.Shared` of `Environment.*` aan vanuit code onder
  test waarop een test assert.
- Laat data niet afhangen van de test-uitvoerings-VOLGORDE. Elke test arrangeert zijn eigen wereld.
- Assert niet op een waarde die het framework randomiseerde, tenzij je de seed vastzette en hem kent.
  Onthoud dat RNG-gebaseerde faker-waarden afhangen van de trek-VOLGORDE: een veld bovenstrooms
  toevoegen verschuift elke latere random-waarde. Verkies stabiele, naam-gebaseerde waarden
  (`xfake.NewGuid("customer-acme")`) of assert op een eigenschap, niet op een exacte random-waarde.
- Laat geen synthetische data lekken waar die voor echt kan worden aangezien. Faker-output (namen,
  e-mails, BSN/IBAN) is fictief maar realistisch — seed het nooit in gedeelde of prod-achtige stores, en
  behandel het niet als veilig om bloot te geven.

---

## 3. Unit-testen met XModelBuilder

Unit-tests oefenen één klasse/methode uit zonder I/O. De taak van XModelBuilder hier is de **Arrange**-
stap een one-liner te maken die zowel realistisch als minimaal is.

### 3.1 Structuur & conventies

- **xUnit**, één testklasse per unit-under-test, één gedrag per `[Fact]`/`[Theory]`.
- Elke test-body draagt de markers `// Arrange`, `// Act`, `// Assert` (de conventie van deze repo;
  combineer markers voor one-liners, bijv. `// Act & Assert`). Block-bodies, geen expression-bodies.
- Benoem tests naar het gedrag: `Method_State_ExpectedOutcome`.
- Geen DI nodig voor pure unit-tests — de statische facades resolven via de procesbrede standalone
  provider:

  ```csharp
  var order = Create.Model<Order>();                                          // basis + cross-cutting
  var vip   = Use.Builder<VipCustomerBuilder>().Build();                      // een specifieke mother
  var many  = Create.Models<Product>(3, (b, i) => b.With(p => p.Sku, $"SKU-{i}")); // drie, per index gevarieerd
  ```

  Voor alles wat registraties vergt (fakers, specifieke builders) bouw je een kleine provider in de test
  of een gedeelde fixture — zie README hoofdstuk 14.

### 3.2 Bouw de Arrange met intentie

```csharp
[Fact]
public void Total_Excludes_Cancelled_Lines()
{
    // Arrange — een mother + precies de delta die er voor DEZE test toe doet
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

De test leest als zijn intentie: *geannuleerde regels tellen niet mee.* Alles wat niet genoemd is (SKU's,
prijzen, adressen) kwam uit de defaults van de builder en is irrelevante ruis die buiten de test blijft.

### 3.3 Do's en don'ts (unit)

**Wel**

- Zet de *canonieke geldige vorm* van een type in één `[ModelBuilder]` en hergebruik die overal.
- Overschrijf alleen waar de test over gaat; laat de defaults de rest dragen.
- Gebruik `BuildMany(n, (b, i) => …)` voor gevarieerde collecties in plaats van gekopieerde `new`'s
  (README hfst. 12).
- Gebruik `ForEmpty<T>()` wanneer je specifiek een kaal object wilt (bijv. het testen van validatie van
  een half-gevulde entiteit) — het slaat de cross-cutting laag over.
- Gebruik `Extend(existing)` om een tweede dataset op een object te leggen zonder de defaults opnieuw te
  draaien.

**Niet**

- `new` geen domeinobjecten met 12 constructor-argumenten inline — het verbergt intentie en breekt bij
  elke modelwijziging.
- Bouw geen reusachtig "god object" in een basisfixture waar elke test stiekem van afhangt. Bouw per
  test; deel alleen de *builder*, niet de *instance*.
- Assert niet op incidentele default-waarden ("City == Amsterdam") tenzij de City het punt is.
- Grijp niet in `internal`/private state om te arrangeren; als een waarde moeilijk te zetten is, is dat
  een ontwerpsignaal.
- Overlaad de defaults van een builder niet. Een waarde waar de helft van je tests stilzwijgend op leunt
  heeft een grote blast radius als hij verandert — houd defaults minimaal en voor de hand liggend, en zet
  wat een test daadwerkelijk nodig heeft.

### 3.4 Test doubles: bouw echte objecten, mock alleen op de grenzen

XModelBuilder neemt de meest voorkomende reden om naar een mock te grijpen weg — een "geldig genoeg"
objectgraph — waardoor de vraag zich toespitst op *collaborators*, niet op data. De vuistregel:

- **Gebruik het echte ding (gebouwd door een builder) voor waarden, entiteiten en pure domeinlogica.**
  Een mock van een domeinobject in elkaar zetten is een smell: bouw het. Echte objecten vangen echte bugs
  die de standaardantwoorden van een mock verbergen.
- **Vervang alleen echte grenzen door een double**: de klok (`TimeProvider`/`FakeTimeProvider`), het
  netwerk/HTTP, het filesystem, message buses, betaal-/e-mailgateways — alles wat traag, extern of
  niet-deterministisch is.
- **Verkies een handgeschreven fake boven een mock-framework** wanneer de double gedrag heeft (een
  in-memory repository, een fake gateway). Reserveer Moq/NSubstitute voor dunne, interactie-checks ("werd
  `Charge` één keer aangeroepen met dit bedrag?").
- **Over-mock niet.** Een test die elke collaborator mockt assert de call-graph van je implementatie, niet
  het gedrag — hij breekt bij elke refactor en bewijst weinig. Veel mocks nodig hebben is het ontwerp dat
  je vertelt dat de unit te veel doet.
- In **integratie**-tests is de grensregel identiek: houd de database en app-bedrading echt (dat is het
  punt), maar stub echt externe services — de demo verruilt echte auth voor `TestAuthHandler`, en een
  betaalprovider zou op dezelfde manier gestubd worden.

### 3.5 Parameterized tests: één gedrag, veel gevallen

Een `[Theory]` houdt "één gedrag per test" vast terwijl hij een tabel van gevallen dekt; de builder
levert de per-geval-delta:

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

Gebruik `[InlineData]` voor een handvol literals, `[MemberData]`/`[ClassData]` wanneer een geval een
gebouwd object of een berekende waarde nodig heeft. Voor een *collectie* die binnen één test varieert,
grijp je naar `BuildMany(n, (b, i) => …)` (README hfst. 12) in plaats van een gekopieerde lijst.

### 3.6 Assertions: expressief, en structureel voor graphs

- **Standaardiseer op één assertion-stijl** per solution. Een fluent-assertion-library (Shouldly,
  AwesomeAssertions, of FluentAssertions — let op: FluentAssertions werd commercieel vanaf v8) leest beter
  en faalt duidelijker dan een kale `Assert.Equal`; de voorbeelden hier blijven op kaal xUnit alleen om er
  geen voor je te kiezen.
- **Vergelijk hele graphs structureel**, niet veld voor veld: `actual.Should().BeEquivalentTo(expected)`
  (of record-gelijkheid) maakt van "assert 20 properties" één intentie-onthullende regel — en past perfect
  bij een builder die de `expected` deterministisch produceerde.
- Voor grote response-DTO's of gegenereerde documenten is **snapshot/approval-testing** (Verify) vaak
  beter dan welke handgeschreven assertion ook; het determinisme van XModelBuilder houdt de snapshots
  stabiel (zie §6).

---

## 4. Integratietesten met XModelBuilder (BDD / Reqnroll)

Integratietests draaien de echte applicatiebedrading — hier de ASP.NET Core API in-process via
`WebApplicationFactory<Program>` — tegen een echte database, gedreven door Gherkin-scenario's. Hier betaalt
structuur zich het meest uit, omdat de suite snel groeit.

### 4.1 Indeling: eerst per DOMEIN, dan per artefact

Groepeer alles wat een domein bezit bij elkaar, zodat een nieuweling "hoe test ik orders?" in één map
vindt. Splits binnen een domein per artefact, **één klasse per bestand**. Gedeelde, domeinloze stukken
wonen in `Common/`; infrastructuur in `Support/`.

```
Tests/
  Common/                       # het gedeelde fundament (hoort bij geen enkel domein)
    Contexts/                   #   CurrentUserContext, HttpResponseContext, ApiResponse, ScenarioState (aggregaat)
    Drivers/                    #   ApiDriver (generieke basis), AuthenticationDriver, ShopDriver (aggregaat)
    Steps/                      #   CommonSteps (auth + generieke asserts), RoleMap
  Domains/
    Ordering/
      Builders/                 #   [ModelBuilder("order")], [ModelBuilder("address")]
      Contexts/                 #   OrderContext
      Drivers/                  #   OrderApiDriver : ApiDriver
      Features/                 #   PlaceOrder.feature, FulfillOrder.feature
      Steps/                    #   PlaceOrderSteps, FulfillOrderSteps
    Catalog/  …
    Customers/ …                #   (een domein mag geen Driver hebben als het alleen in-memory bouwt)
  Support/
    Infrastructure/             #   CustomWebApplicationFactory, ShopTestHost, TestDatabase, TestAuthHandler
    Seeding/                    #   DatabaseSeeder
    ShopModelBuilders.cs        #   de ENE XModelBuilder-registratie, hergebruikt door beide DI-lagen
    ScenarioDependencies.cs     #   scenario-DI composition root (Reqnroll MS DI-plugin)
    DatabaseHooks.cs, HostHooks.cs
  TestParallelization.cs
```

**Waarom domein-eerst?** Feature-files, de steps die ze binden, de drivers die ze aanroepen en de builders
die ze gebruiken veranderen allemaal samen. Ze bij elkaar zetten houdt een wijziging aan "ordering" binnen
`Domains/Ordering/` en maakt hergebruik van steps voor de hand liggend (je kijkt eerst in het domein, dan
in `Common/`).

### 4.2 De bouwstenen

**Scenario-contexts** — kleine, muteerbare, scoped state-dragers, ÉÉN per domein, plus een optioneel
aggregaat. Een step schrijft wat hij produceerde; een latere step leest het. Houd ze dom (properties + een
`Require()`-guard), geen logica.

```csharp
// Common/Contexts/HttpResponseContext.cs — gedeelde "laatste response"
public sealed class HttpResponseContext
{
    public ApiResponse? Last { get; set; }
    public ApiResponse Require() => Last ?? throw new InvalidOperationException("No API call has been made yet.");
}
```

De optionele **aggregaat-context** (`ScenarioState`) bundelt de per-domein-contexts voor de zeldzame step
die meerdere domeinen omspant — maar een enkel-domein-step injecteert alleen de context van zijn eigen
domein.

**Drivers** — het "hoe" van het praten met het systeem, zodat steps declaratief blijven. Een **generieke
basis** bezit het loodgieterswerk (HTTP + JSON + auth, het vastleggen van de laatste response);
**specifieke drivers** drukken alleen endpoints uit.

```csharp
// Common/Drivers/ApiDriver.cs — loodgieterswerk één keer
public abstract class ApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
{
    protected Task<ApiResponse> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);
    // …hangt test-auth-headers aan, legt response.Last vast…
}

// Domains/Ordering/Drivers/OrderApiDriver.cs — alleen endpoints
public sealed class OrderApiDriver(HttpClient c, CurrentUserContext u, HttpResponseContext r) : ApiDriver(c, u, r)
{
    public Task<ApiResponse> PlaceOrder(PlaceOrderRequest request) => PostAsync("/api/orders", request);
    public Task<ApiResponse> Pay(int orderId)                       => PostAsync($"/api/orders/{orderId}/pay");
}
```

**Steps** — dunne lijm: bouw de request met XModelBuilder, roep een driver aan, leg vast/assert via een
context. De `PlaceOrder`-step is drie regels omdat de `"order"`-builder de adressen en betaling vult,
zodat de Gherkin-tabel alleen de regels draagt:

```csharp
// Domains/Ordering/Steps/PlaceOrderSteps.cs
[When(@"I place the following order:")]
public async Task WhenIPlaceTheOrder(Table table)
{
    var request = xprovider.For<PlaceOrderRequest>("order").CreateModel(table); // tabel -> request-model
    await orders.PlaceOrder(request);
}
```

```gherkin
When I place the following order:
    | Field             | Value       |
    | Lines[0].Sku      | SKU-PHONE-1 |
    | Lines[0].Quantity | 2           |
```

**Storage-helpers & seeding** — bouw de baseline-dataset met DEZELFDE builders waarmee je test, zodat de
seed realistisch is en meebeweegt met het model. Customers worden geseed via hun rol-specifieke genaamde
builders — de library "dogfood't" zichzelf op de seed:

```csharp
// Support/Seeding/DatabaseSeeder.cs
db.Customers.Add(xprovider.For<Customer>("customer").With(c => c.Email, "alice@shop.test")…Build());
db.Customers.Add(xprovider.For<Customer>("admin").With(c => c.Email, "admin@shop.test")…Build());
```

### 4.3 Isolatie: één dure setup, goedkope reset per scenario

Het patroon dat een database-gedragen suite zowel realistisch als snel houdt:

1. **Eén keer per run** (`[BeforeTestRun]`, `HostHooks` → `ShopTestHost`): creëer het schema, open ÉÉN
   gedeelde `SqlConnection`, bouw de `WebApplicationFactory`, en **commit** de seed als de baseline.
2. **Eén keer per scenario** (`[BeforeScenario]`/`[AfterScenario]`, `DatabaseHooks`): begin een transactie
   op de gedeelde connectie, draai het scenario, en **draai hem daarna terug** — waarmee de store wordt
   gereset naar de gecommitte seed zonder het schema aan te raken.

```csharp
// Support/DatabaseHooks.cs
[BeforeScenario(Order = 0)] public void Begin()    => HostHooks.Instance.Database.BeginScenarioTransaction();
[AfterScenario(Order = 0)]  public void Rollback() => HostHooks.Instance.Database.RollbackScenarioTransaction();
```

Belangrijke infrastructuurkeuzes (`Support/Infrastructure/TestDatabase.cs`):

- **Eén fysieke connectie** gedeeld door testcode ÉN de in-process API, zodat één transactie beide omvat —
  en er nooit een tweede connectie meedoet, zodat de transactie nooit naar MSDTC wordt gepromoveerd (wat
  LocalDB niet ondersteunt).
- Omdat de seed gecommit is maar de writes van het scenario niet, kun je ze in SSMS inspecteren via
  `READ UNCOMMITTED` terwijl je op een breakpoint gepauzeerd staat.
- **Scenario's draaien sequentieel** — de gedeelde connectie is niet thread-safe
  (`TestParallelization.cs` schakelt xUnit-parallellisatie uit). Parallellisme, als je het nodig hebt,
  gaat op een grovere korrel: shard per *assembly* / testproject, of geef elke worker zijn eigen database.

### 4.4 De drie DI-lagen (houd ze uit elkaar)

1. **Applicatie-DI** — `Program.cs`. Het echte werk; fork het niet in tests.
2. **Test-basis-DI** — `CustomWebApplicationFactory.ConfigureTestServices`: herbedraad `DbContext` naar de
   gedeelde test-connectie, verruil echte auth voor een header-gebaseerde `TestAuthHandler`, registreer
   XModelBuilder + seeded fakers voor de seeder.
3. **Scenario-DI** — `ScenarioDependencies` (Reqnrolls MS DI-plugin): een verse container per scenario die
   de per-domein-contexts en drivers registreert (`AddScoped`), plus zijn eigen XModelBuilder-provider
   (een andere seed) voor het bouwen van request-modellen in steps.

XModelBuilder wordt één keer geregistreerd in `ShopModelBuilders.AddShopModelBuilders(seed)` en hergebruikt
door zowel de test-basis- als de scenario-laag, zodat "hoe een Customer wordt gebouwd" op precies één plek
gedefinieerd is.

> **Isolatie-knop.** Als je scenario's met een scope-per-scenario draait en wilt dat elke scope zijn eigen
> provider + fakers + seeded RNG's krijgt, registreer dan met `AddXModelBuilder(isolation:
> XModelBuilderIsolation.PerScope)` (README hfst. 21.1). De demo gebruikt `Shared` omdat zijn reset de
> DB-transactie is, niet de container.

### 4.5 Gherkin ↔ XModelBuilder

- **Verticale `Field | Value`-tabel → één model:** `xprovider.For<T>("name").CreateModel(table)`.
- **Horizontale tabel → één model per rij:** `xprovider.CreateModels<T>(table)` (of `(table, "name")`).
- **Deep-paths in cellen** (`Lines[0].Sku`, `Address.City`) laten één compacte tabel een graph beschrijven.
- **Named-builder-referenties** resolven automatisch voor reference-getypeerde members in een cel, zodat
  een tabel `order` kan zeggen en de `"order"`-builder krijgt.
- **Componeer over steps heen met `Extend`:** bouw een Customer als persoon in één step, voeg dan in latere
  steps een verzend- en factuuradres toe via `Extend` — zonder de defaults van de builder opnieuw te
  draaien (README hfst. 18; `Domains/Customers`).

### 4.6 Do's en don'ts (integratie)

**Wel**

- Houd steps declaratief; duw alle mechaniek in drivers en alle state in contexts.
- Hergebruik `Common/`-steps voor cross-cutting concerns (auth, "ik word als verboden afgewezen") in plaats
  van ze per feature opnieuw te implementeren.
- Assert via de publieke oppervlakte (HTTP-status + response-DTO). Lees alleen rechtstreeks uit de DB om
  een side-effect te verifiëren die de API niet blootgeeft.
- Maak de seed de *minimale* geloofwaardige wereld; laat scenario's toevoegen wat ze nodig hebben.
- Schrijf negatieve scenario's (401/403/409) naast het happy path — dat zijn de goedkoopste bugs om te
  vangen.

**Niet**

- Deel geen muteerbare instances tussen scenario's; de transactie-reset dekt alleen de database.
- Zet geen asserts in drivers of businesslogica in steps.
- Laat het ene scenario niet afhangen van een ander dat eerst gedraaid moet zijn.
- Reik niet voorbij de transactie (bijv. een background-service op zijn eigen connectie) en verbaas je dan
  waarom de rollback "niet werkte".
- Laat niet één `CommonSteps` god-class groeien; promoveer naar `Common/` alleen wat echt gedeeld is, houd
  de rest in het eigenaardomein.

---

## 5. De suites bootstrappen voor een nieuw groot project

Een dag-één-checklist die schaalt van de eerste feature tot honderden:

1. **Maak twee testprojecten**: `<Product>.UnitTests` en `<Product>.IntegrationTests` (xUnit, net10.0,
   `Nullable`/`ImplicitUsings` aan).
2. **Voeg XModelBuilder + een faker toe** en één `AddXModelBuilder`-registratiemodule die overal
   hergebruikt wordt (`XModelBuilders.cs`). Registreer de **cross-cutting laag** meteen met je
   identiteits-/audit-defaults — doe het voordat iemand een builder schrijft, zodat het nooit vergeten
   wordt.
3. **Leg de mapconventie vast** (§4.1) leeg-maar-aanwezig: `Common/{Contexts,Drivers,Steps}`, `Domains/`,
   `Support/{Infrastructure,Seeding}`. Conventie wint van een wiki-pagina.
4. **Zet de integratie-host één keer op**: `WebApplicationFactory`, een `TestDatabase` (gedeelde
   connectie), `HostHooks`/`DatabaseHooks` voor de run-/scenario-levenscyclus, een `TestAuthHandler` voor
   header-gebaseerde auth, en een `DatabaseSeeder` die de baseline bouwt met jouw builders.
5. **Kies je databasestrategie** (§6) en bedraad de reset (transactie-rollback, of DB per worker).
6. **Schrijf de eerste verticale slice end-to-end** voor één domein — feature → steps → driver → builder →
   context — en behandel het als het sjabloon dat elk later domein kopieert.
7. **Zet CI-discipline aan** vanaf commit #1: snelle unit-tests altijd; DB-gebonden integratietests achter
   een container of op een self-hosted agent; nultolerantie voor flakes.

Het punt van het framework hier is dat stappen 2, 6 en de seed allemaal ÉÉN definitie van je testdata delen,
zodat de suite consistent begint en consistent blijft naarmate het model groeit.

---

## 6. Moderne praktijken & nieuwere inzichten

- **Verkies in-process boven out-of-process.** `WebApplicationFactory<Program>` geeft je echte routing,
  auth, filters en DI op ~unit-testsnelheid, en laat één transactie test + server omvatten. Reserveer volle
  externe stacks voor een dunne smoke-suite.
- **Databasekeuze is een spectrum.** LocalDB (zoals in de demo) is nul-infra op Windows en
  SSMS-inspecteerbaar. **Testcontainers** (SQL Server / Postgres in Docker) is de portable, CI-vriendelijke
  keuze en maakt parallellisme per worker mogelijk (een DB per container). Een in-memory provider is alleen
  goed voor logica die geen provider-specifieke SQL raakt — hij liegt over relationeel gedrag, dus vertrouw
  hem niet voor integratiedekking.
- **Beheer de klok.** Injecteer overal `TimeProvider`; gebruik `FakeTimeProvider` in tests en drijf
  tijdafhankelijke defaults door de cross-cutting laag. Time-travel-scenario's worden dan data, geen
  `Thread.Sleep`.
- **Respecteer de test-piramide, maar investeer in het "trophy"-midden.** Veel snelle unit-tests, een
  sterke band in-process-integratietests (hoogste bug-per-minuut), weinig echte end-to-end-tests.
- **Assert gedrag, en overweeg approval/snapshot-testing** (bijv. Verify) voor grote response-DTO's of
  gegenereerde documenten — het maakt van "assert 30 velden" één beoordeelde snapshot. Combineer met het
  determinisme van XModelBuilder zodat snapshots stabiel zijn.
- **Parallellisme is een data-isolatiebeslissing, geen schakelaar.** Parallelliseer alleen wanneer elke
  worker zijn eigen data bezit (aparte DB/schema/tenant). De demo ruilt parallellisme in voor een
  gedeelde-connectie-transactie-reset; een Testcontainers-per-worker-opzet ruilt setup-kosten in voor
  parallellisme. Kies bewust.
- **Flaky-test-hygiëne.** Geen `[Retry]` als middel. Quarantaine, reproduceer met een vaste seed, fix de
  grondoorzaak (meestal gedeelde state, echte tijd, of volgorde). Volg flake-rate als kwaliteitsmetriek.
- **Contract-first request-builders.** Bouw de request-DTO's van de API met XModelBuilder (zie de
  `"order"`/`PlaceOrderRequest`-builder), niet de domeinentiteiten, voor API-tests — je test dan het
  contract dat je clients echt gebruiken.
- **Coverage is een diagnose, geen doel.** Hoge regeldekking van oppervlakkige asserts bewijst niets; jaag
  op gedrag. Om te meten of je asserts regressies echt vangen, verkies je mutation testing (Stryker.NET)
  boven een coverage-percentage als kwaliteitspoort.
- **Categoriseer en scheid suites zodat CI ze onafhankelijk kan draaien.** Gebruik aparte projecten (unit
  vs integratie) en/of xUnit `[Trait("Category","Integration")]` gefilterd met `dotnet test --filter`.
  Snelle tests bewaken elke push; trage DB-gebonden tests draaien waar de infrastructuur bestaat.
- **Let op async-hygiëne.** Async-tests zijn `async Task`, nooit `async void` (een `async void`-test kan
  niet ge-await worden en zijn fouten gaan stil verloren); blokkeer nooit met `.Result`/`.Wait()` in tests —
  het geeft deadlocks en begraaft de echte fout. Await de driver, zoals de step-voorbeelden doen.
- **Houd testcode op productiestandaard.** Eén klasse per bestand, XML-doc het gedeelde fundament (drivers,
  contexts, infrastructuur), review het als productiecode. De suite is een product; een groot project leeft
  of sterft bij hoe onderhoudbaar zijn tests zijn.

---

## 7. Waar je hierna kijkt

- [`Demo/README-nl.md`](../Demo/README-nl.md) — het volledig uitgewerkte voorbeeld waarnaar deze gids
  verwijst.
- README hfst. 5 (basis + cross-cutting laag + genaamde builders), hfst. 12 (`BuildMany`),
  hfst. 14 (standalone facades), hfst. 18 (Reqnroll/SpecFlow + `Extend`), hfst. 21.1 (isolatie).
- [`docs/scenarios/`](scenarios/) — gerichte, uitvoerbare data-vormende scenario's (time-travel,
  gerelateerde graphs, een deterministische Nederlandse dataset).
- [`docs/adr/`](adr/) — de ontwerpbeslissingen achter het resolutiemodel.
