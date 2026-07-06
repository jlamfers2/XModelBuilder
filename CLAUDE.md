# CLAUDE.md

Guidance for working in this repository. Keep it short; deep detail lives in `README-nl.md`.

## Wat is dit

XModelBuilder is een reflectiegebaseerd .NET-framework voor het bouwen van
deterministische testdata (Object Mother + Test Data Builder + orchestration).
Eén generieke basisklasse `ModelBuilder<TBuilder,TModel>` levert voor elke klasse
een fluente builder; properties/fields worden gezet via lambda's, string-deep-paths
(`"Adres.Straat"`, `"Regels[2].Aantal"`) of `WithValues` (key/value, o.a. Gherkin-tabellen).
Tekstwaarden worden culture-aware geconverteerd en ondersteunen een mini-datataal
voor arrays/sets/dictionaries/object-literals. Integreert met Bogus en met
Reqnroll/SpecFlow. Werkt met MS DI én standalone via een statische provider.

**`README-nl.md` is de canonieke, volledige spec (hoofdstukken 1–21).** Raadpleeg het
voor API-details, algoritmes en randgevallen; werk het bij als gedrag verandert.

## Solution-layout

| Project | Rol |
|---|---|
| `XModelBuilder` | Kernlibrary (net10.0) |
| `XModelBuilder.Fakers.XFaker` | Dependency-vrije deterministische faker (`AddXFaker(seed)`) |
| `XModelBuilder.Fakers.Bogus` | Bogus-integratie (`AddBogusFaker(seed)`) |
| `XModelBuilder.Reqnroll` / `.SpecFlow` | Gherkin-tabel-integraties (`CreateModel(s)<T>`) |
| `*.UnitTests`, `XModelBuilder.Fakers.UnitTests` | xUnit-testprojecten |
| `XModelBuilder.Benchmarks` | Benchmarks |
| `Demo/XModelBuilder.Demo.Shop` | Demo-webshop (ASP.NET Core Web API, EF Core/SQL Server) |
| `Demo/XModelBuilder.Demo.Shop.IntegrationTests` | Reqnroll-integratietests van de demo (zie `Demo/README.md`) |
| `__old_samples` | Verouderd; niet gebruiken |

## Kernstructuur van het hoofdproject

- Root: publieke contracten en kernimpl — `IModelBuilder(.cs)`, `ModelBuilder.cs`,
  `IModelBuilderProvider.cs`, `ModelBuilderOptions.cs`, `ModelBuilderAttribute.cs`,
  `IFaker.cs`, `*Extensions.cs`.
- `Default/`: statische facades (`For`, `Use`, `Create`), `DefaultModelBuilder`,
  `DefaultModelBuilderProvider` (standalone singleton).
- `DependencyInjection/`: `ServiceCollectionExtensions` (`AddXModelBuilder`, `AddModelBuilder`,
  `UseAsDefaultModelBuilder`, `AddFaker`, …), `ModelBuilderProvider` (**enige plek met echte
  resolutielogica**), `AssemblyScanner`, `ModelBuilderDefaults`, `XModelBuilderIsolation`.
- `Core/` (alles `internal`, behalve publieke `FriendlyNameExtensions`): `Parser`/`DataParser`/
  `CharScanner` (mini-datataal), `ValueConverter` (conversie + faker-tokens), `FakerInvoker`
  (overload-resolutie + Type/IServiceProvider-injectie), `StringPathSetter`/`LambdaPathSetter` (deep-paths),
  `Instantiator`, `HelperExtensions`.

## Conventies

- Doelplatform **net10.0**, `Nullable` enable, `ImplicitUsings` enable — voor alle projecten.
- Tests: **xUnit**. Nieuwe kernfeatures krijgen tests in `XModelBuilder.UnitTests`;
  faker/Gherkin-features in het bijbehorende `*.UnitTests`-project.
- **Elke** unittest heeft in zijn body de comments `// Arrange`, `// Act` en `// Assert`
  (in die volgorde), die de drie fasen markeren. Dit geldt voor iedere bestaande én nieuw
  toegevoegde test. Gebruik een block-body (geen expression-body) zodat de comments passen;
  als een fase samenvalt met een andere (bv. een one-liner die aanroept én assert), combineer
  dan de markers, bv. `// Act & Assert`.
- **Nieuw toegevoegde files** krijgen Engelstalige XML-doc-headers op **alle `public` en
  `protected` members** (types, methods, properties, constructors, fields): `<summary>`, plus
  `<param>`/`<typeparam>` voor elke parameter/typeparameter, `<returns>` bij een returnwaarde,
  en `<exception>` voor elke exceptie die als onderdeel van het contract wordt gegooid. `internal`/
  `private` members mogen kort of ongedocumenteerd blijven. (Bestaande files vul je aan wanneer je
  ze toch aanraakt.) **Uitzondering:** test-projecten (`*.UnitTests`) — daar volstaan sprekende
  testnamen + de AAA-comments; XML-doc-headers op testmethodes/-helpers zijn niet nodig.
- `XModelBuilder` heeft `InternalsVisibleTo` naar `XModelBuilder.UnitTests`, dus `internal`
  types (Core-laag) zijn daar direct testbaar.
- Kernproject hangt bewust aan de **volledige** `Microsoft.Extensions.DependencyInjection`
  (niet alleen `.Abstractions`), omdat `DefaultModelBuilderProvider` zelf een ServiceProvider bouwt.
- Elke builder voor een modeltype heeft een verplichte, unieke `[ModelBuilder("naam")]`;
  de default wijs je order-onafhankelijk aan met `UseAsDefaultModelBuilder<TBuilder>()`.
- Tekst is grotendeels Nederlands (README, foutmeldingen). Houd die taal aan.

## Bouwen & testen (PowerShell)

```powershell
dotnet build XModelBuilder.sln
dotnet test  XModelBuilder.sln                      # alle testprojecten
dotnet test  XModelBuilder.UnitTests                # alleen kern
```

## Git

De projectmap is (nog) geen git-repository. Voer geen commits uit tenzij expliciet gevraagd;
`git init` alleen op verzoek.
