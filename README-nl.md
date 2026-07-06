# XModelBuilder - Gebruikershandleiding & Technische Specificatie

[![CI](https://github.com/jlamfers2/XModelBuilder/actions/workflows/ci.yml/badge.svg)](https://github.com/jlamfers2/XModelBuilder/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/jlamfers2/XModelBuilder/branch/main/graph/badge.svg)](https://codecov.io/gh/jlamfers2/XModelBuilder)
[![NuGet](https://img.shields.io/nuget/v/XModelBuilder.svg?logo=nuget)](https://www.nuget.org/packages/XModelBuilder)
[![Downloads](https://img.shields.io/nuget/dt/XModelBuilder.svg?logo=nuget)](https://www.nuget.org/packages/XModelBuilder)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Deterministische testdata voor .NET — zonder voor elke klasse een builder te schrijven.**

XModelBuilder geeft je out of the box een fluent Test Data Builder voor *elke* C#-klasse:
constructor-parameters, init-only properties, read-only members, private backing
fields — het werkt gewoon. Configureer waarden met sterk getypeerde lambda's in code, of
met complete Gherkin-tabellen in je BDD-scenario's, en laat seeded fakers (inclusief
Bogus) de rest invullen, volledig reproduceerbaar.

```csharp
var order = xprovider.For<Order>()
    .With(x => x.Id, provider => provider.XFake().NewGuid())
    .With(x => x.Customer.Name, provider => provider.Bogus().Company.CompanyName())
    .With(x => x.OrderDate, new DateTime(2026, 7, 1))
    .With(x => x.Lines[0].Product, provider => provider.Use<MyProductBuilder>().Build())
    .With(x => x.Lines[0].Quantity, 3)
    .Build();
```

BDD-tests aan het schrijven? Voer een Gherkin-tabel rechtstreeks in een model — dot-paden,
indexers, typeconversie en faker-tokens werken allemaal binnen de tabel:

```gherkin
Given the following order:
  | Id              | Customer.Name               | OrderDate  | Lines[0].Product | Lines[0].Quantity |
  | xfake.NewGuid() | bogus.company.companyName() | 2026-07-01 | MyProduct        | 3                 |
```

```csharp
[Given("the following order:")]
public void GivenTheFollowingOrder(Table table)
    => _order = _xprovider.For<Order>().CreateModel(table);
```

**Waarom XModelBuilder?**

- 🧱 Eén generieke basisklasse bouwt *elk* model — geen handgeschreven builders nodig
- 🎲 Deterministisch van opzet: seeded fakers, stabiele naam-gebaseerde GUID's, door `TimeProvider` gestuurde datums
- 📋 First-class Reqnroll- & SpecFlow-integraties: Gherkin-tabellen worden objectgrafen
- 🔤 Een mini-datataal maakt van gewone strings arrays, dictionaries en geneste objecten
- 🔌 Werkt met `Microsoft.Extensions.DependencyInjection` *of* volledig standalone

```
Install-Package XModelBuilder
```

De seeded fakers uit de voorbeelden hierboven worden geleverd als losse packages (zie hoofdstuk 21):

```
Install-Package XModelBuilder.Fakers.XFaker   # xfake.*-tokens + .XFake()-extensie
Install-Package XModelBuilder.Fakers.Bogus    # bogus.*-tokens + .Bogus()-extensie
```

## Over dit document

Dit document beschrijft (1) hoe je XModelBuilder gebruikt als consument van de
library, en (2) hoe de library intern werkt, tot op het niveau van algoritmes
en grammatica's. Doel van deel (2) is dat dit document ook dienst kan doen als
specificatie om een vergelijkbaar framework vanaf nul te (laten) bouwen,
zonder de bestaande broncode te moeten lezen.

Doelplatform: .NET 10 (C#), Nullable reference types aan, ImplicitUsings aan.
Naast de kernlibrary (project XModelBuilder) bevat de solution twee losse
integratieprojecten voor Gherkin-test-frameworks: XModelBuilder.Reqnroll en
XModelBuilder.SpecFlow (zie hoofdstuk 18).

## Inhoudsopgave

1.  [Wat is XModelBuilder?](#1-wat-is-xmodelbuilder)
2.  [Installatie en registratie (Dependency Injection)](#2-installatie-en-registratie-dependency-injection)
3.  [Snel starten](#3-snel-starten)
4.  [Kernconcepten en publieke API](#4-kernconcepten-en-publieke-api)
5.  [Meerdere builders per modeltype: ModelBuilderAttribute en resolutievolgorde](#5-meerdere-builders-per-modeltype-modelbuilderattribute-en-resolutievolgorde)
6.  [De "With"-methoden in detail](#6-de-with-methoden-in-detail)
7.  [Deep-paths: geneste members en collecties via string-paden](#7-deep-paths-geneste-members-en-collecties-via-string-paden)
8.  [Constructor-argumenten: hoe XModelBuilder ze herkent](#8-constructor-argumenten-hoe-xmodelbuilder-ze-herkent)
9.  [De mini-datataal voor string-waarden (arrays/objecten als tekst)](#9-de-mini-datataal-voor-string-waarden)
10. [ValueConverter: conversieregels, tokens, named builders en culture](#10-valueconverter-conversieregels-tokens-named-builders-en-culture)
11. [Fakers: IFaker, registratie, tokens en getypeerd aanroepen](#11-fakers-ifaker-registratie-tokens-en-getypeerd-aanroepen)
12. [BuildMany: meerdere instances in één keer bouwen (en Extend: bouwen op een bestaande instance)](#12-buildmany-meerdere-instances-in-één-keer-bouwen)
13. [Eigen ModelBuilders schrijven](#13-eigen-modelbuilders-schrijven)
14. [Statisch gebruik zonder DI-container (DefaultModelBuilderProvider)](#14-statisch-gebruik-zonder-di-container)
15. [Build-algoritme, instantiatie-fallbacks en randgevallen](#15-build-algoritme-instantiatie-fallbacks-en-randgevallen)
16. [Architectuur / bestandsoverzicht](#16-architectuur--bestandsoverzicht)
17. [Volledige API-referentie (signatures)](#17-volledige-api-referentie-signatures)
18. [Gherkin-integratie: Reqnroll en SpecFlow](#18-gherkin-integratie-reqnroll-en-specflow)
19. [Bekende beperkingen](#19-bekende-beperkingen)
20. [Specificatie-samenvatting (voor het naprogrammeren van dit framework)](#20-specificatie-samenvatting-voor-het-naprogrammeren-van-dit-framework)
21. [Deterministisch genereren met een seed (XFaker en BogusFaker)](#21-deterministisch-genereren-met-een-seed-xfaker-en-bogusfaker)

## 1. Wat is XModelBuilder?

XModelBuilder is een reflectiegebaseerd framework voor het bouwen en genereren van deterministische testdata in .NET. Het combineert de patronen Object Mother en Test Data Builder met een orchestration-laag, zodat je op een fluent manier objectgrafen kunt samenstellen zonder voor iedere modelklasse handmatig builders te hoeven schrijven.

Het framework ondersteunt zowel handmatig geconfigureerde als automatisch gegenereerde testdata en kan worden geïntegreerd met faker-libraries zoals Bogus, waarvan de integratie standaard wordt meegeleverd. XModelBuilder fungeert daarbij als centrale orchestrator die builders, gegenereerde gegevens en scenario-specifieke configuratie samenbrengt tot reproduceerbare en onderhoudbare testdatasets.

XModelBuilder is ontworpen voor gebruik in unit-, integratie- en acceptatietests. Dankzij de integratie met Reqnroll en SpecFlow sluit het bovendien naadloos aan op BDD-scenario's.


Belangrijkste eigenschappen:

- Eén generieke basisklasse (`ModelBuilder<TBuilder,TModel>`) die voor élke
  klasse een builder levert, inclusief klassen met constructor-parameters,
  read-only properties, init-only properties en private backing fields.
- Properties/fields kunnen worden gezet via:
  - Strongly-typed lambda-expressies: `x => x.Naam`
  - String-paden met dot-notatie en array/list-indexering, voor toepassing in b.v. Gherkin tabel gegevens: `"Adres.Straat"`,
    `"Regels[2].Aantal"`
  - Eén grote set key/value-paren (`WithValues`), bv. afkomstig van een
    configuratiebestand, testdata-tabel of Gherkin-tabel.
- Eenvoudige tekstuele waarden (`"42"`, `"true"`, `"Maandag"`) worden automatisch
  geconverteerd naar het juiste .NET-type van de property (int, bool, enum,
  DateTime, Guid, ...), inclusief culture-aware parsing.
- Tekstuele waarden ondersteunen ook arrays (`"[1,2,3]"` of zelfs kortweg
  `"1,2,3"`), `HashSet<T>`, `Dictionary<TKey,TValue>` en geneste object-literals
  (`"{Straat:\"Hoofdstraat\",Nummer:\"1\"}"`) om complete geneste modellen en
  collecties te bouwen vanuit een enkele string (hoofdstuk 9).
- Speciale tokens `null()`, `new()` en `default()` (en hun escaped vorm, zie
  hoofdstuk 10) geven fijnmazige controle over hoe een waarde wordt
  geproduceerd. Daarnaast kun je voor complexe (model-)types met een bare
  naam verwijzen naar een SPECIFIEKE, met `[ModelBuilder("naam")]` getagde
  builder (zie hoofdstuk 5 en 10).
- Eigen "fakers" (methoden op een klasse die `IFaker` implementeert) zijn
  aanroepbaar als `"naam(args)"`-token, mét overloading en optionele
  automatische Type-/IServiceProvider-injectie voor generieke fixture-
  methoden. Dezelfde fakers zijn ook GETYPEERD aanroepbaar (`Faker<TFaker>()`,
  of gewoon rechtstreeks via constructor-injectie) - zie hoofdstuk 11.
- `BuildMany` bouwt in één keer meerdere instances: op de builder (herhaalde
  `Build()`-aanroepen, gedeelde basisconfiguratie) of op de provider (elk een
  verse builder, optioneel per-index of via een specifiek genaamde builder)
  - zie hoofdstuk 12.
- Er kunnen MEERDERE builders voor hetzelfde modeltype geregistreerd worden;
  elke builder krijgt een verplichte, unieke `[ModelBuilder("naam")]` waarmee je
  hem expliciet opvraagt, en de default wijs je order-onafhankelijk aan met
  `UseAsDefaultModelBuilder<TBuilder>()` (hoofdstuk 5).
- Werkt zowel met `Microsoft.Extensions.DependencyInjection` als volledig
  standalone (zonder DI-container) via een statische provider.
- Apart te installeren integraties voor Reqnroll en SpecFlow bouwen modellen
  rechtstreeks vanuit Gherkin-tabellen (hoofdstuk 18).

## 2. Installatie en registratie (Dependency Injection)

Voeg een projectverwijzing naar XModelBuilder toe en registreer de services:

```csharp
using XModelBuilder.DependencyInjection;

services.AddXModelBuilder();
```

Optioneel met configuratie van culture-instellingen:

```csharp
services.AddXModelBuilder(options =>
{
    options.DefaultCulture  = CultureInfo.GetCultureInfo("nl-NL");
    options.DateTimeCulture = CultureInfo.GetCultureInfo("nl-NL");
});
```

`AddXModelBuilder` doet drie dingen:

1. Registreert `ModelBuilderOptions` (via `Configure`, of met fabrieksdefaults
   als er geen configuratie-delegate is meegegeven). Standaardwaarden:
   `DefaultCulture = CultureInfo.InvariantCulture`,
   `DateTimeCulture = CultureInfo.InvariantCulture`.
2. Registreert een "keyed" fallback-implementatie voor het open generic
   type `IModelBuilder<>` onder de key `"default"`, geïmplementeerd door
   `XModelBuilder.Default.DefaultModelBuilder<T>` (een builder die niets
   bijzonders doet in `SetDefaults()`). Hierdoor kan voor ELK modeltype T
   waarvoor je niets specifieks hebt geregistreerd, toch een werkende
   builder worden opgelost.
3. Registreert `IModelBuilderProvider`
   (`XModelBuilder.DependencyInjection.ModelBuilderProvider`) - standaard als
   Singleton, of als Scoped wanneer je `AddXModelBuilder(isolation:
   XModelBuilderIsolation.PerScope)` meegeeft (zie hoofdstuk 21.1 voor wanneer je
   dat wilt - bv. één scope per BDD-scenario).

Wil je voor een specifiek modeltype een eigen builder gebruiken (bijvoorbeeld
om altijd bepaalde defaults te zetten), registreer die dan extra:

```csharp
services.AddModelBuilder<PersonBuilder>();
// of
services.AddModelBuilder(typeof(PersonBuilder));
```

Je kunt dit MEERDERE KEREN doen voor hetzelfde modeltype: alle geregistreerde
builders blijven beschikbaar (zowel via `For<TModel>()` als via expliciete
naam-resolutie). Zie hoofdstuk 5 voor hoe XModelBuilder bepaalt welke builder
"de" builder is wanneer er meer dan één geregistreerd staat voor hetzelfde
modeltype.

Wil je alle `IModelBuilder`-implementaties automatisch laten registreren
(handig voor grotere apps met veel builders verspreid over assemblies):

```csharp
services.AddModelBuildersFromAssembly(typeof(SomeMarkerType).Assembly); // één assembly
services.AddModelBuildersFromAssemblies();                              // hele AppDomain (via AssemblyScanner)
```

Dit scant op alle niet-abstracte, niet-generieke types die `IModelBuilder`
implementeren, en registreert elk via `AddModelBuilder(type)`. Omdat de resolutie
order-onafhankelijk is (hoofdstuk 5), maakt de scan-volgorde niet uit; vergeet bij
≥2 builders per type niet de default te kiezen met `UseAsDefaultModelBuilder` en
het geheel te controleren met `ValidateXModelBuilderRegistrations()`.

Voor fakers is er BEWUST geen scanning - registreer die expliciet met
`AddFaker<T>()` (hoofdstuk 11).

## 3. Snel starten

Voorbeeldmodel:

```csharp
public class Address
{
    public string Street { get; set; }
    public string City   { get; set; }
}

public class Person
{
    public Person(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    private readonly string _name = null!;
    public string Name { get => _name; }
    public string City { get; init; } = null!;
    public string[] Options { get; }
    public Address Address { get; }
}
```

Bouwen via lambda-expressies (sterk getypeerd):

```csharp
var person = xprovider.For<Person>()
    .With(x => x.Name, "John")
    .With(x => x.City, "Amsterdam")
    .With(x => x.Options, ["noot"])
    .With(x => x.Address, b => b
        .With(a => a.Street, "Hoofdstraat")
        .With(a => a.City, "Amsterdam"))
    .Build();
```

Een builder-instantie kun je ook eerst opvragen en dan apart `Build()`-en, en
het resultaat als waarde meegeven - functioneel identiek aan de vorige regel:

```csharp
var address = xprovider.Use<ComplexAddressBuilder>().Build();
var person = xprovider.For<Person>()
    .With(x => x.Name, "John")
    .With(x => x.Address, address)
    .Build();
```

Bouwen via string-paden (bijvoorbeeld vanuit een testdata-tabel of CSV):

```csharp
var person = xprovider.For<Person>()
    .With("Name", "John")
    .With("City", "Amsterdam")
    .With("Options", "[noot]")
    .With("Address", "{Street:\"Hoofdstraat\",City:\"Amsterdam\"}")
    .Build();
```

Of - als er een builder genaamd `"complex-adres"` geregistreerd staat voor
`Address` - door simpelweg die naam als waarde te geven:

```csharp
var person = xprovider.For<Person>()
    .With("Name", "John")
    .With("Address", "complex-adres")
    .Build();
```

Beide voorbeelden produceren een geldig `Person`-object, ook al heeft `Person`
geen publieke parameterloze constructor, een read-only `Name` (alleen een
private backing field), een init-only `City` en een read-only `Address` die door
de constructor wordt gevuld.

Zonder DI-container (statische facade, zie hoofdstuk 14):

```csharp
using XModelBuilder.Default;

var person = Create.Model<Person>(); // bouwt met alle defaults
var custom = For.Model<Person>().With(x => x.Name, "Jane").Build();
```

## 4. Kernconcepten en publieke API

**`IModelBuilder<TModel>`**
Sterk getypeerde builder-interface voor één modeltype. Methoden:
`Reset`, `With` (4 overloads), `WithValues`, `Build`, `Extend` (bouwen op een
bestaande instance, hoofdstuk 12.1). Zie hoofdstuk 17.

**`IModelBuilder`**
Niet-generieke "schaduw"-interface met dezelfde mogelijkheden, maar met
`LambdaExpression`/`object` in plaats van `Expression<Func<TModel,TValue>>`/
`TValue`. Hierdoor kan code die het modeltype niet compile-time kent (zoals
de provider die op basis van een Type werkt) toch met een builder werken.

**`IModelBuilderProvider`**
Lost builders op. Methoden: `For<TModel>()`, `For(Type)`, `For<TModel>(name)`,
`For(Type,name)`, `Use<TModelBuilder>()`, `Use(Type)`. `For` zoekt een builder
OP BASIS VAN HET MODELTYPE (met of zonder expliciete naam, zie hoofdstuk
5); `Use` geeft je een specifieke, compile-time bekende builder-klasse
rechtstreeks terug, ongeacht wat er voor het modeltype geregistreerd is.

**`ModelBuilder<TBuilder, TModel>`**
Abstracte basisklasse die `IModelBuilder<TModel>` en `IModelBuilder`
implementeert. Hier zit alle logica (constructor-detectie, deep-paths,
conversie). Eigen builders erven hiervan over en implementeren alleen
de abstracte methode `SetDefaults()`.

**`ModelBuilderAttribute`**
Geeft een concrete builderklasse een VERPLICHTE, per-modeltype UNIEKE naam
(`[ModelBuilder("naam")]`), waarmee hij expliciet opvraagbaar is (zie hoofdstuk
5). De naam bepaalt NIET welke builder de default is - dat configureer je
order-onafhankelijk met `UseAsDefaultModelBuilder<TBuilder>()` en controleer je
met `ValidateXModelBuilderRegistrations()`.

**`ModelBuilderOptions`**
- `DefaultCulture` (`CultureInfo`, default: `InvariantCulture`) - gebruikt voor
  alle conversies behalve DateTime/DateTimeOffset.
- `DateTimeCulture` (`CultureInfo`, default: `InvariantCulture`) - gebruikt
  specifiek voor DateTime/DateTimeOffset-parsing.

## 5. Meerdere builders per modeltype: ModelBuilderAttribute en resolutievolgorde

Normaal gesproken registreer je hoogstens één builder per modeltype. Soms wil
je echter meerdere "varianten" van een builder voor hetzelfde modeltype
beschikbaar hebben (bijvoorbeeld een eenvoudige en een uitgebreide variant),
en toch een eenduidige "default" aanwijzen. De resolutie is BEWUST
order-onafhankelijk: ze hangt nooit af van registratievolgorde, ook niet
wanneer builders via assembly-scanning uit meerdere assemblies binnenkomen.

**`[ModelBuilder("naam")]`**
Een attribuut op een concrete builderklasse (een klasse die van
`ModelBuilder<TBuilder,TModel>` afleidt). De naam is VERPLICHT en moet UNIEK
zijn per modeltype. De naam bepaalt NIET de default (er is geen speciale naam
`"default"` meer).

```csharp
[ModelBuilder("complex-adres")]
public sealed class ComplexAddressBuilder(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xprovider)
    : ModelBuilder<ComplexAddressBuilder, Address>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(x => x.Street, "Hoofdstraat");
    }
}
```

Registreer zo veel builders voor `Address` als je wilt, en wijs - bij meer dan
één - expliciet de default aan met `UseAsDefaultModelBuilder<TBuilder>()`
(non-generic variant: `UseAsDefaultModelBuilder(typeof(...))`). Het modeltype
wordt uit de builder afgeleid, dus geen magische string:

```csharp
services
    .AddModelBuilder<ComplexAddressBuilder>()       // [ModelBuilder("complex-adres")]
    .AddModelBuilder<SimpleAddressBuilder>()         // [ModelBuilder("simpel")]
    .UseAsDefaultModelBuilder<SimpleAddressBuilder>(); // 'simpel' is de default voor Address
```

Resolutie van `xprovider.For<TModel>()` / `xprovider.For(Type)`:

1. **0 builders** voor dat modeltype → de generieke open-generic fallback
   (`DefaultModelBuilder<>`, of een via `SetDefaultModelBuilder` aangepaste
   fallback - zie hoofdstuk 14).
2. **1 builder** → die ene (een geconfigureerde default is niet nodig).
3. **≥2 builders** → de met `UseAsDefaultModelBuilder` geconfigureerde default.
   Is er geen default geconfigureerd, dan wordt een `InvalidOperationException`
   gegooid (geen stille keuze, geen "laatste wint").

Wil je EXPLICIET een specifiek genaamde builder gebruiken, ongeacht de default:

```csharp
xprovider.For<Address>("complex-adres")        // of
xprovider.For(typeof(Address), "complex-adres") // of
xprovider.Use<ComplexAddressBuilder>()
```

Dit zoekt de builder met EXACT die (unieke) naam, hoofdletter-ongevoelig en
volledig order-onafhankelijk. Bestaat zo'n naam niet, dan een
`KeyNotFoundException` - er is GEEN stille fallback naar gewone data-conversie.

**Validatie.** Roep na alle registraties `ValidateXModelBuilderRegistrations()`
aan (op de `IServiceCollection`, of `Validate()` op de standalone provider) om
de regels in één keer af te dwingen: elke builder heeft een `[ModelBuilder]`-
naam, namen zijn uniek per modeltype, en elk modeltype met ≥2 builders heeft een
geconfigureerde, daadwerkelijk geregistreerde default. Alle overtredingen worden
samen in één `InvalidOperationException` gerapporteerd.

```csharp
services.ValidateXModelBuilderRegistrations(); // gooit bij dubbele naam / ontbrekende default
```

Deze resolutie werkt zowel via de DI-provider
(`XModelBuilder.DependencyInjection.ModelBuilderProvider`, gebaseerd op
Microsoft.Extensions.DependencyInjection's `GetServices(...)`) als via de
statische `DefaultModelBuilderProvider`.

Dezelfde naam kan ook ALS STRINGWAARDE gebruikt worden bij `With(string,string)`
om een geneste, complexe property te bouwen via een specifiek genaamde
builder - zie hoofdstuk 10 ("named builder reference").

## 6. De "With"-methoden in detail

`IModelBuilder<TModel>` kent de volgende manieren om een waarde te zetten:

**a) `With<TValue>(Expression<Func<TModel,TValue>> getter, TValue? value)`**
Zet een waarde direct. Werkt op zowel ondiepe (`x => x.Naam`) als diepe
paden (`x => x.Adres.Straat`) en op array/list-indexering
(`x => x.Regels[0].Aantal`). De waarde mag ook het resultaat zijn van een
los opgevraagde builder, bv. `xprovider.Use<ComplexAddressBuilder>().Build()`.

**b) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<TValue?> valueFactory)`**
Als (a), maar de waarde wordt lazy berekend op het moment van `Build()`
(niet op het moment van de `With`-aanroep).

**c) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder) where TValue : class`**
Voor geneste modellen: geeft je een builder voor het type van de
geneste property, die je verder configureert; het resultaat van diens
`Build()` wordt de waarde. Intern is dit niets anders dan variant (b) met
`() => builder(xprovider.For<TValue>()).Build()` als value-factory.

**d) `With(string memberPath, string? value)`**
Zet een waarde via een tekstueel pad (zie hoofdstuk 7) en een tekstuele
waarde die via `ValueConverter` naar het juiste type wordt geconverteerd
(zie hoofdstuk 10) - inclusief de `null()`/`new()`/`default()`-tokens en
named-builder-references voor complexe types.

**e) `WithValues(IEnumerable<KeyValuePair<string,string?>> values)`**
Verwerkt een hele set paden/waarden in één keer (bijvoorbeeld een rij uit
een datatabel of Gherkin-tabel, zie hoofdstuk 18). Elke entry wordt los
beoordeeld: als de key exact (zonder punt) overeenkomt met een
constructor-parameter, wordt die als constructor-argument gebruikt;
anders wordt het een deep-path-instelling.

**f) `WithBuilder<TValue>(Expression<Func<TModel,TValue>> getter, string builderName) where TValue : class`**
Lambda-equivalent van de named-builder-reference-syntax (hoofdstuk 5/10):
zet de property op het resultaat van het bouwen van de builder die
geregistreerd staat onder `[ModelBuilder(builderName)]` voor `TValue`, dus
functioneel gelijk aan `With(getter, () => xprovider.For<TValue>(builderName).Build())`.
Dit is BEWUST een eigen methode, geen overload van `With`: een generieke
`With<TValue>(getter, string)` overload zou ambigu zijn met
`With<TValue>(getter, TValue value)` zodra `TValue` zelf `string` is (en dat
is precies het meest voorkomende `With`-patroon, bv. `With(x => x.Naam, "John")`).

**g) `With<TValue>(Expression<Func<TModel,TValue>> getter, Func<IModelBuilderProvider,TValue?> valueFactory)`**
Als (b), maar de factory krijgt de builder's EIGEN `IModelBuilderProvider`
(`_xprovider`) als argument, in plaats van dat je die uit een omsluitende
scope moet closure-capturen. Dat is meer dan syntactische suiker: een
HERBRUIKBARE factory-functie (bv. een setje "fake value"-factories dat je
los van een specifieke test/provider deelt) loopt anders het risico een
VEROUDERDE of VERKEERDE provider vast te pakken in scenario's met scoped
DI of parallelle testruns met elk hun eigen `IServiceProvider`. Met deze
vorm krijgt de factory ALTIJD de juiste provider voor déze builder:

```csharp
.With(x => x.Address, provider => provider.Faker<AddressFakers>().Random())
```

Geen overload-ambiguïteit met vorm (b): `Func<TValue?>` en
`Func<IModelBuilderProvider,TValue?>` zijn altijd te onderscheiden op het
aantal lambda-parameters.

**`Reset()`**
Wist alle eerder opgegeven `With`-instellingen en constructor-argumenten,
en roept `SetDefaults()` opnieuw aan. Handig om dezelfde builder-instantie
te hergebruiken voor meerdere, licht-verschillende modellen.

**`Build()`**
Maakt het model (zie hoofdstuk 15) en past daarna alle deep-path-
instellingen toe, in de volgorde waarin ze zijn opgegeven.

## 7. Deep-paths: geneste members en collecties via string-paden

Een string-pad bestaat uit één of meer met een punt gescheiden segmenten.
Elk segment is een membernaam, optioneel gevolgd door `"[index]"` om een
array- of lijstelement aan te spreken.

```
"Naam"                      -> top-level member
"Adres.Straat"               -> geneste member
"Regels[2].Aantal"           -> 3e regel (index 2), member "Aantal"
"Regels[2]"                  -> 3e regel zelf (geen verdere member)
```

Regels voor padresolutie (geldt voor zowel de lambda-variant als de
string-variant):

- Memberresolutie is hoofdletter-ongevoelig en kijkt naar zowel publieke als
  niet-publieke properties en fields.
- Een property komt alleen in aanmerking als hij een setter heeft (`CanWrite`).
  Heeft hij die niet (bv. een auto-property met alleen een getter), dan zoekt
  XModelBuilder naar een backing field, in deze volgorde:
  1. Een field met exact dezelfde naam als de member (`"_naam"` als de member
     letterlijk `"_naam"` heet, niet `"Naam"`).
  2. Een field genaamd `"_" + membernaam` (bv. `"_naam"` voor member `"Naam"`).
  3. Het door de compiler gegenereerde backing field van een auto-property:
     `"<Naam>k__BackingField"`.

  De eerste match die bestaat wordt gebruikt. Bestaat geen van deze, dan
  wordt een `InvalidOperationException` gegooid (`"Unable to set ..."`).
- Voor een niet-laatste segment dat geen index heeft: als de huidige waarde
  van die member null is, wordt automatisch een nieuwe instantie gebouwd via
  de geconfigureerde `IModelBuilderProvider` voor het membertype (dus geneste
  objecten "auto-vivify" on demand) en toegewezen, voordat er verder wordt
  afgedaald.
- Voor het laatste segment zonder index: de string-waarde wordt via
  `ValueConverter` naar het deklaratietype van de member geconverteerd en
  toegewezen.
- Voor een geïndexeerd segment op een array: is de array null of te kort voor
  de gevraagde index, dan wordt een nieuwe, grotere array aangemaakt
  (bestaande elementen worden gekopieerd) en terug toegewezen aan de member.
- Voor een geïndexeerd segment op een `IList` (`List<T>`, etc.): is de member
  null, dan wordt eerst een lijst gebouwd (via de provider, of - in de
  lambda-variant - via `Activator` voor interface-/lijsttypes) en toegewezen;
  vervolgens wordt de lijst aangevuld tot minstens `index+1` elementen lang
  (toegevoegde elementen: `default(T)` voor het laatste segment, anders
  via de provider gebouwde instanties).
- Bij verder afdalen na een geïndexeerd, niet-laatste segment, wordt gebruik
  gemaakt van het ACTUELE runtime-type van het element (niet het statische
  collectie-elementtype), zodat polymorfe elementen correct verder kunnen
  worden bewerkt.

De lambda-variant (`x => x.Regels[2].Aantal`) ondersteunt dezelfde mechanismen,
maar leest de structuur uit een C#-expressie-boom in plaats van een string.
Ondersteunde knooppunten: `MemberExpression` (member access), `IndexExpression`
en `ArrayIndex` (array-/lijst-indexering), en `MethodCallExpression` naar
`get_Item` (als fallback voor indexer-aanroepen die niet als `IndexExpression`
worden gemodelleerd). Alleen een ENKEL, CONSTANT, GEHEEL indexargument wordt
ondersteund (geen berekende of variabele indices, geen meervoudige indexer-
argumenten). Conversies aan het begin van de expressie (zoals `x => (object)
x.Naam`) worden automatisch genegeerd.

## 8. Constructor-argumenten: hoe XModelBuilder ze herkent

Veel modelklassen hebben properties die alleen via de constructor kunnen
worden gezet (geen setter, geen backing field, of bewust immutable design).
XModelBuilder ondersteunt dit door bij élke `With`-aanroep eerst te checken of
de aanroep een constructor-argument representeert, vóórdat hij hem als
deep-path-instelling wegschrijft.

Een `With`-aanroep wordt als constructor-argument behandeld als:

1. Het modeltype GEEN "standaard activator" gebruikt. Dat wil zeggen: de
   gekozen constructor (zie hoofdstuk 15) heeft minstens één verplicht
   (niet-optioneel) parameter. Heeft de constructor geen parameters, of
   zijn ze allemaal optioneel, dan wordt het model altijd via een
   gewone parameterloze `Activator.CreateInstance` gemaakt en is er dus
   niets om als constructor-argument te binden.
2. Het pad TOP-LEVEL en zonder punt is (dus `"Adres"`, niet `"Adres.Straat"`),
   EN de naam (hoofdletter-ongevoelig) overeenkomt met de naam van één van
   de parameters van de gekozen constructor.

Voldoet een `With`-aanroep hieraan, dan wordt de waarde (of value-factory)
opgeslagen in een interne tabel, gekoppeld aan de bijbehorende
`ParameterInfo`. Bij het bouwen van het model (zie hoofdstuk 15) wordt voor
elke constructor-parameter eerst gekeken of er zo'n opgeslagen waarde is;
zo niet, dan wordt de eigen default-waarde van de parameter gebruikt
(of null).

Belangrijk: een pad MET een punt dat toevallig met een constructor-parameter-
naam begint (bv. `"Adres.Straat"` terwijl er een parameter `address` bestaat)
wordt NIET als constructor-argument behandeld - dat blijft een deep-path-
instelling die pas NA constructie wordt toegepast. Dit betekent dat als de
betreffende member geen setter en geen vindbaar backing field heeft, en er
ook geen losse `"Adres"`-instelling is opgegeven om de constructor te voeden,
het bouwen van het model met een exception faalt (de constructor krijgt dan
null/default voor dat argument).

Stringwaarden die als constructor-argument zijn opgeslagen, worden ten tijde
van `Build()` (niet eerder) via `ValueConverter` geconverteerd naar het
parameter-type, met de `DateTimeCulture`/`DefaultCulture` van de builder - dus
ook hier werken `null()`/`new()`/`default()` en named-builder-references gewoon
(bv. `.With("Address", "complex-adres")` voor een ctor-only `Address`-property).

## 9. De mini-datataal voor string-waarden

String-waarden die arrays, lijsten of geneste objecten representeren, worden
geparsed met een kleine, eigen taal (vergelijkbaar met, maar niet gelijk aan,
JSON). Hieronder de grammatica in EBNF-achtige notatie:

```
value      := string | array | object | bareValue
string     := '"' { charOrEscape } '"'
charOrEscape := escape | <any character except '"' or '\'>
escape     := '\' ( '\' | '"' | 'n' | 'r' | 't' )
array      := '[' [ value { ',' value } ] ']'
object     := '{' [ pair { ',' pair } ] '}'
pair       := (string | bareValue) ':' value
bareValue  := { <any character not in reserved set> }
reserved   := '[' | ']' | '"' | '{' | '}' | ',' | ':' | ' ' | '\'
              | CR | LF | TAB
```

Aanvullende regels:

- Whitespace (spatie, tab, CR, LF) tussen tokens is altijd insignificant en
  wordt overgeslagen.
- Object-keys worden hoofdletter-ongevoelig vergeleken met de membernamen
  van het doeltype.
- Een "bareValue" is simpelweg alle tekst tot het volgende gereserveerde
  teken; er is geen escaping nodig voor bare values (gebruik dan strings).
- Op het TOP-NIVEAU van een array-conversie (dus wanneer een hele string-
  waarde wordt geconverteerd naar een array- of lijst-type) zijn de
  omsluitende vierkante haken OPTIONEEL: zowel `"1,2,3"` als `"[1,2,3]"` zijn
  geldig en leveren hetzelfde resultaat op. Binnen geneste structuren
  (bijvoorbeeld een array-element dat zelf een array is) zijn de haken
  WEL verplicht, omdat anders niet te onderscheiden is waar het ene element
  stopt en het volgende begint.
- Objecten (`"{...}"`) zijn op elk niveau altijd verplicht volledig omsloten
  door accolades; er is geen "bare object"-variant.

Voorbeelden:

```
"42"                                  -> bareValue "42"
"[1,2,3]" of "1,2,3"                  -> array van drie bareValues
"[[1,2],[3,4,5]]"                      -> array van twee arrays
"{Straat:\"Hoofdstraat\",Nummer:1}"   -> object met twee velden
"{Adres:{Straat:\"Hoofdstraat\"}}"    -> geneste objecten
"[{Waarde:1},{Waarde:2}]"             -> array van object-literals
```

Dezelfde grammatica wordt ook gebruikt voor twee .NET-collectietypes die niet
via members worden opgebouwd, maar als HEEL doeltype worden geconverteerd
(zie hoofdstuk 10, stappen 7-8):

- `Dictionary<TKey,TValue>` / `IDictionary<TKey,TValue>`: de "object"-vorm
  (`"{...}"`) wordt geïnterpreteerd als key/value-paren in plaats van als
  ledenamen van een POCO, bv. `"{a:1,b:2}"` voor `Dictionary<string,int>`.
- `HashSet<T>` / `ISet<T>`: de "array"-vorm (haakjes optioneel op het top-
  niveau) wordt geïnterpreteerd als de elementen van de set, bv. `"1,2,3"`
  of `"[1,2,3]"` voor `HashSet<int>`.

Tuples (`Tuple<...>`/`ValueTuple<...>`) worden momenteel NIET ondersteund - zie
hoofdstuk 19.

## 10. ValueConverter: conversieregels, tokens, named builders en culture

Elke string-waarde die aan een member of constructor-parameter wordt
toegewezen, doorloopt de volgende stappen, in deze volgorde:

1. Trim de input.
2. **ESCAPING**: bepaal of de getrimde input begint met precies één `'@'`. Zo ja,
   verwijder dat ene leidende `'@'`-teken en sla stappen 3 en 4 hieronder
   over - de rest van de tekst wordt vanaf hier altijd als LETTERLIJKE data
   behandeld, nooit als token/faker-aanroep/named-builder-naam. Voorbeeld:
   `"@null()"` levert de letterlijke tekst `"null()"` op (voor een
   string-property), niet de waarde null. Wil je het `'@'`-teken zelf NA het
   escapen behouden, gebruik dan twee leidende @'s (`"@@new()"`) - na het
   verwijderen van precies één leidend `'@'` blijft `"@new()"` over, wat -
   omdat we al "isEscaped" zijn - simpelweg als letterlijke tekst
   wordt behandeld.
3. (alleen als niet ge-escaped) Vergelijk de input EXACT met de speciale
   tokens:
   - `"null()"` -> retourneer null (voor elk doeltype).
   - `"new()"` -> retourneer `Instantiator.CreateInstance(doeltype)`: een
     "kale" instantie, gemaakt met de meest permissieve
     constructor-strategie (zie hoofdstuk 15), ZONDER een
     geregistreerde ModelBuilder te gebruiken en zonder ooit
     een exception te gooien.
   - `"default()"` -> hangt af van het doeltype:
     - `Nullable<T>` -> null
     - `string` -> null
     - overige value-types -> `default(T)` (via `Activator`)
     - overige reference-types (klassen) -> `provider.For(doeltype).Build()`:
       bouwt via de builder die op dat moment als "default" geldt voor
       dat type (zie hoofdstuk 5) - dus inclusief eventuele
       `SetDefaults()`-logica.
4. (alleen als niet ge-escaped, en geen match in stap 3) **FAKER-AANROEP**: komt
   de input overeen met het patroon `"naam(args)"` (een identifier, direct
   gevolgd door haakjes, eindigend op `')'`), dan wordt dit opgevat als een
   aanroep van een geregistreerde `IFaker`-methode (zie hoofdstuk 11):
   retourneer `provider.InvokeFaker(naam, args, doeltype, culture)`. Dit geldt
   voor ELK doeltype (ook value-types en string), in tegenstelling tot de
   named-builder-reference (stap 12) die alleen voor complexe referentie-
   types geldt.
5. Is het doeltype `string`, retourneer de (eventueel ge-escapete) tekst
   direct.
6. Is de (overgebleven) input leeg: retourneer null voor nullable/reference-
   doeltypes, of gooi een `ArgumentException` voor non-nullable value-types.
7. Is het doeltype `Dictionary<TKey,TValue>` of `IDictionary<TKey,TValue>`:
   verwacht een object-literal (`'{...}'`, zie hoofdstuk 9); elk key/value-paar
   wordt key->TKey (via deze zelfde Convert-functie) en value->TValue
   (recursief via `ConvertObject`) geconverteerd. Begint de input niet met
   `'{'`, dan wordt een `FormatException` gegooid.
8. Is het doeltype `HashSet<T>` of `ISet<T>`: parse de input met de
   array-grammatica (haakjes optioneel op het top-niveau, zie hoofdstuk 9)
   en converteer elk element recursief naar T; resultaat is een `HashSet<T>`.
9. Is het doeltype een array: parse de input met de in hoofdstuk 9
   beschreven array-grammatica en converteer elk element recursief naar het
   elementtype.
10. Is het doeltype `List<T>`, `IList<T>`, `ICollection<T>` of `IEnumerable<T>`:
    zelfde aanpak, resultaat is een `List<T>`.
11. Begint de (resterende) input met `'{'`: parse als object-literal (zie
    hoofdstuk 9) en bouw een instantie van het doeltype: er wordt eerst een
    "lege" instantie gebouwd via `provider.For(doeltype).Build()`, waarna voor
    elke key/value in het object-literal de overeenkomstige member (zelfde
    resolutieregels als hoofdstuk 7) wordt gezocht en de waarde - recursief
    geconverteerd naar het type van die member - wordt toegewezen.
12. **NAMED BUILDER REFERENCE**: is de input NIET ge-escaped, EN is het doeltype
    géén value-type, géén string en géén object (`typeof(object)`), dan wordt
    de (resterende) input opgevat als de NAAM van een `[ModelBuilder(naam)]`-
    getagde builder voor dat doeltype (zie hoofdstuk 5): retourneer
    `provider.For(doeltype, input).Build()`. Is er geen builder met die naam
    geregistreerd voor dat type, dan wordt een `KeyNotFoundException` gegooid -
    er is GEEN stille fallback naar de stappen hieronder.
13. Is het doeltype een enum: parseer op naam (hoofdletter-ongevoelig) of op
    numerieke waarde. (Dit punt - en de twee hieronder - worden in de
    praktijk alleen nog bereikt voor value-types, string of object, of voor
    ge-escapete input op een reference-type, omdat stap 12 anders al een
    builder-naam-lookup forceert of een exception gooit.)
14. Is er een bekende type-converter geregistreerd voor het doeltype
    (zie hieronder), gebruik die.
15. Anders: probeer `System.Convert.ChangeType` met de gegeven culture.
16. Lukt geen van de bovenstaande stappen, dan wordt elke uitzondering
    opgevangen en opnieuw gegooid als een `FormatException` met een duidelijke
    melding (`"Cannot convert X to target type Y. Missing converter for Y?"`).

Bekende, ingebouwde type-converters (allemaal culture-aware, met
ondersteuning voor duizendtal-scheidingstekens bij gehele getallen):

```
bool, byte, short, int, long, float, double, decimal,
DateTime, DateTimeOffset, TimeSpan, Guid, char
```

Je kunt deze set uitbreiden of overschrijven met:

```csharp
ValueConverter.AddKnownTypeConverter(typeof(MyType), (text, culture) => ...);
```

LET OP: dit is een PROCESBREDE, statische registratie (niet gebonden aan een
specifieke `IModelBuilderProvider`- of `ModelBuilderOptions`-instantie).

Culture-keuze: `DateTime` en `DateTimeOffset` gebruiken altijd
`ModelBuilderOptions.DateTimeCulture`; alle andere typen gebruiken
`ModelBuilderOptions.DefaultCulture`. Dit stelt je in staat om bijvoorbeeld
Nederlandse datumnotaties (dd-MM-jjjj) te combineren met punt-decimale
getallen, of omgekeerd.

Samenvatting van de drie tokens, faker-aanroepen en named-builder-references,
met hun escaped (letterlijke) tegenhanger:

| Token | Betekenis | Escaped (letterlijk) |
|---|---|---|
| `null()` | expliciet null | `@null()` |
| `new()` | kale instantie (Instantiator, geen builder) | `@new()` |
| `default()` | bouw via de huidige "default"-builder (of CLR-default voor value-types/string) | `@default()` |
| `naam(args)` | roep IFaker-methode "naam" aan met args (elk doeltype; zie hoofdstuk 11) | `@naam(args)` |
| `<naam>` | bouw via de builder getagd `[ModelBuilder(<naam>)]` voor dat type (alleen voor niet-string referentietypes) | `@<naam>` |

## 11. Fakers: IFaker, registratie, tokens en getypeerd aanroepen

Voor testdata die niet vast hoeft te liggen (leeftijden, namen, willekeurige
tekst, ...) schrijf je een gewone klasse met methoden die `IFaker` implementeert.
Die methoden zijn op TWEE manieren aanroepbaar: dynamisch via een
`"naam(args)"`-token in de mini-taal (`With(string,string)`/`WithValues`/Gherkin-
tabellen), of GETYPEERD rechtstreeks in C#-code.

**`IFaker`**
Een lege marker-interface. Elke klasse die `IFaker` implementeert en
geregistreerd wordt, levert zijn (niet-private, niet-generieke) instance-
methoden op als mogelijke `"naam(args)"`-tokens (zie "Zichtbaarheidsregels"
hieronder), én is in zijn geheel getypeerd op te vragen.

```csharp
public class PersonFakers : IFaker
{
    public DateTime AgeBetween(int minYears, int maxYears) =>
        DateTime.Today.AddYears(-Random.Shared.Next(minYears, maxYears + 1));

    public string RandomString() => "...";
    public string RandomString(int length) => "...";   // overload
}
```

Registreren (DI):

```csharp
services.AddXModelBuilder()
    .AddFaker<PersonFakers>();                          // default: Singleton
    // of: .AddFaker<PersonFakers>(ServiceLifetime.Scoped) - bv. om een
    //     per-scope geseede Random/Bogus-Faker via de constructor van
    //     PersonFakers te injecteren, voor reproduceerbare testdata.
```

Fakers registreer je BEWUST altijd expliciet met `AddFaker<T>()` - er is geen
assembly-scanning voor fakers. Ze zijn doorgaans met weinig (zelden meer dan een
paar), dus expliciet houden is overzichtelijker en voorkomt verrassende
volgorde-afhankelijkheid. (Model builders zijn in grotere apps wél talrijk; dáár
is scanning er wel - zie `AddModelBuildersFromAssemblies()`, hoofdstuk 2/5.)

`AddFaker` registreert de faker ZOWEL onder zijn eigen concrete type als
(forwarding naar diezelfde instance/scope) onder `IFaker` - de eerste vorm is
voor getypeerd gebruik (hieronder), de tweede voor de dynamische token-
dispatch. Dit betekent dat je een faker ook gewoon als GEWONE DEPENDENCY kunt
injecteren, zonder ooit via XModelBuilder te gaan:

```csharp
public class PersonSteps(PersonFakers fakers)   // gewone constructor-injectie
{
    public void Foo() => fakers.AgeBetween(1, 20);
}
```

Registreren (standalone, zonder DI - zie hoofdstuk 14):

```csharp
DefaultModelBuilderProvider.Current.AddFaker(new PersonFakers());        // kant-en-klare instance
// of, om de container zelf te laten construeren (met eigen dependencies):
DefaultModelBuilderProvider.Current
    .AddServices(s => s.AddSingleton<SomeDependency>())
    .AddFaker<PersonFakers>();
```

Gebruik via tokens (overal waar een string-waarde wordt geconverteerd):

```csharp
.With("Birthday", "AgeBetween(1,20)")
.With("Name", "RandomString(5)")
.With("Name", "RandomString()")     // andere overload, op aantal args
```

Gebruik GETYPEERD, rechtstreeks in C#:

```csharp
var age = xprovider.Faker<PersonFakers>().AgeBetween(1, 20);
// standalone/ambient-variant (zie hoofdstuk 14):
var age2 = Use.Faker<PersonFakers>().AgeBetween(1, 20);
```

`Faker<TFaker>()` (op `IModelBuilderProvider`) is het getypeerde tegenhanger
van de token-syntax: geeft de geregistreerde TFaker-instance terug (dezelfde
instance als via gewone constructor-injectie, voor Scoped/Singleton), met
volledige IntelliSense/compile-time-controle. Bestaat er geen registratie
voor TFaker, dan wordt een `KeyNotFoundException` gegooid.

Resolutieregels (token-dispatch):

- Methode-matching is HOOFDLETTER-ONGEVOELIG, net als de rest van de library.
- Is er voor naam "X" een methode geregistreerd op MEERDERE `IFaker`-klassen,
  dan "wint" de LAATST GEREGISTREERDE klasse die een methode met die naam
  heeft VOLLEDIG (consistent met de `[ModelBuilder]`-resolutievolgorde uit
  hoofdstuk 5) - er worden geen overloads van verschillende klassen gemengd.
- Binnen die klasse wordt een overload als "passend" beschouwd wanneer het
  aantal opgegeven argumenten tussen het aantal VERPLICHTE (niet-optionele) en
  het TOTALE aantal data-parameters ligt (zie hieronder voor de
  Type-/IServiceProvider-parameter-uitzondering) - OPTIONELE parameters mogen
  dus worden weggelaten en worden dan met hun default-waarde gevuld - en wanneer
  elk opgegeven argument succesvol naar het parametertype kan worden
  geconverteerd (via dezelfde ValueConverter-conversie als overal elders). Van
  de passende overloads wint die met de EXACTE ariteit, anders die met de minste
  in te vullen defaults. Past geen enkele overload, dan wordt een
  `MissingMethodException` gegooid; bestaat de naam helemaal niet, dan een
  `KeyNotFoundException`.
- Het returntype van de faker-methode (vaak `object`) wordt na de aanroep:
  ongewijzigd doorgegeven als het al van het juiste/compatibele type is;
  herparsed via deze zelfde Convert-functie als het een string is die nog
  naar een ander doeltype moet; anders ongewijzigd doorgegeven (een eventuele
  type-mismatch leidt dan tot een gewone toewijzingsfout op het member).
- `"naam(args)"`-syntax is universeel beschikbaar (ook voor primitieve
  doeltypes), in tegenstelling tot de named-builder-reference (kale naam,
  geen haakjes) die alleen voor complexe referentietypes geldt - de
  aan/afwezigheid van haakjes maakt de twee syntaxen altijd onderscheidbaar.
- Net als de andere tokens kan `"naam(args)"` worden geëscaped met één leidend
  `'@'`-teken als je de letterlijke tekst wilt (bv. omdat toevallige data
  exact op een faker-aanroep lijkt).

Automatische Type-/IServiceProvider-injectie:

Heeft de gekozen methode één of meer LEIDENDE parameters van EXACT het
type `System.Type` en/of `IServiceProvider` (puur op TYPE gematcht, niet op
parameternaam; in willekeurige volgorde ONDERLING), dan worden die
parameters NIET geteld als token-argument en automatisch gevuld: `Type` met
het doeltype waar op dat moment naar geconverteerd wordt, `IServiceProvider`
met de IServiceProvider van de provider die de aanroep deed (bij DI: de
container zelf; bij de standalone provider: zijn eigen interne, lazy
opgebouwde container - zie hoofdstuk 14). Zo kan een generieke "geef me een
fake waarde van het juiste type, met toegang tot andere services"-methode
geschreven worden:

```csharp
public object Fixture(Type type, IServiceProvider services) => ...
```

aangeroepen als token `"fixture()"` (nul argumenten - `Type` en `IServiceProvider`
komen niet uit de tokentekst, maar uit de context). De volgorde van de twee
parameters maakt niet uit: `(Type, IServiceProvider)` en
`(IServiceProvider, Type)` werken identiek.

Zichtbaarheidsregels (welke methoden tellen mee voor token-dispatch):

- PUBLIC, PROTECTED, INTERNAL en PROTECTED INTERNAL methoden zijn allemaal
  bruikbaar - zowel INSTANCE- als STATIC methoden. Dit laat je toe om
  "framework-georiënteerde" overloads (zoals de Type/IServiceProvider-
  variant hierboven) bewust PROTECTED te maken: dan zijn ze WEL via een
  token aanroepbaar, maar NIET via de getypeerde `Faker<TFaker>()`-route (waar
  gewone C#-toegankelijkheidsregels gelden - een aanroeper buiten de klasse
  kan een protected member toch al niet aanroepen, dus dit wordt afgedwongen
  door de taal zelf, niet door extra framework-code). Een STATIC methode
  heeft geen instance-state nodig, maar de klasse moet nog steeds als
  (instance-)faker geregistreerd zijn (`AddFaker<T>()`/`AddFaker(instance)`) om
  via reflectie "gevonden" te kunnen worden - voor getypeerd gebruik heb je
  voor een static methode XModelBuilder sowieso niet nodig, je roept hem
  gewoon rechtstreeks aan op de klasse (`MyFakers.SomeStaticMethod()`).
- PRIVATE methoden (instance EN static) tellen NOOIT mee voor token-dispatch.
- GENERIC methoden (open generic method definitions, bv. `T Create<T>()`)
  tellen NOOIT mee voor token-dispatch - er bestaat geen tokensyntax om een
  typeargument inline op te geven, en de Type-parameter-auto-injectie
  hierboven dekt het "geef me het juiste type"-scenario al voor gewone,
  niet-generieke methoden. Generic methoden zijn dus UITSLUITEND bedoeld
  voor getypeerd aanroepen:

```csharp
xprovider.Faker<MyFakers>().Create<Address>()
```

Deep-path faker-resolutie (geneste member-paden):

Een token-naam mag óók een met punten gescheiden MEMBER-PAD zijn dat begint bij
een geregistreerde faker, in plaats van één enkele methodenaam. Het eerste
segment kiest de eigenaar-faker (de faker die een member met die naam heeft);
de tussensegmenten worden gelezen als property/field of als parameterloze
methode; en het laatste segment wordt aangeroepen als methode - of, als er geen
methode met die naam bestaat én er geen argumenten zijn meegegeven, gelezen als
property/field (de "terminal-property-fallback"):

```csharp
.With("Name", "bogus.name.firstname()")    // Bogus.Faker -> Name-dataset -> FirstName()
.With("City", "bogus.address.city()")
.With("Name", "bogus.person.firstname()")  // terminal is een property -> via fallback gelezen
```

Hierdoor heb je de hele oppervlakte van een onderliggend object (zoals een Bogus
`Faker`) beschikbaar zónder voor elke generator een adapter-methode te schrijven:
de faker hoeft alleen het object als property te exposen (zie hoofdstuk 21). Het
eerste-segment-pad geeft meteen een namespace, dus zulke tokens botsen niet met
je andere fakers. De last-registered-wint- en overload-/optionele-parameter-
regels hierboven gelden onverkort voor het laatste segment. (Een combinatie als
`x.currency().code` - methode gevolgd door property als laatste stap - is niet
uitdrukbaar: het laatste segment is precies één member; gebruik daarvoor de
getypeerde route.)

**Namespace-conventie (aanbevolen standaard).** Geef elke faker zijn EIGEN
namespace: expose één leesbaar member waarvan de NAAM de namespace van de faker
is en dat het object met de methodes teruggeeft, en spreek de methodes daardoor
aan. De twee ingebouwde fakers doen dit: XFaker exposet `XFake` (tokens
`xfake.nextid()`, hoofdstuk 21.2) en de Bogus-integratie exposet `Bogus` (tokens
`bogus.name.firstname()`, hoofdstuk 21.3). Omdat het eerste padsegment de
eigenaar-faker kiest, voorkomt een namespace-per-faker dat tokens botsen en houdt
het het top-niveau schoon. Een eigen faker MAG methodes nog steeds op het
top-niveau zetten (bv. `AgeBetween(1,20)`), maar een namespace geven is de
aanbevolen default.

## 12. BuildMany: meerdere instances in één keer bouwen

Twee `BuildMany`'s, op twee verschillende plekken, voor twee verschillende
scenario's: één op de BUILDER (dezelfde instance hergebruikt), één op de
PROVIDER (elke instance een verse builder).

**a) Op `IModelBuilder<TModel>` - dezelfde, al geconfigureerde builder:**

```csharp
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count,
    Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
```

Roept `Build()` simpelweg `count` keer aan op DEZELFDE builder. Alles wat je
al via `With(...)` had gezet (lambda-waarden, literals) wordt voor ELKE
instance herbruikt; alles wat via een value-factory of een string-pad-
token (inclusief faker-aanroepen) is gezet, wordt bij ELKE `Build()`-
aanroep OPNIEUW geëvalueerd - dus dat deel kan wél per instance variëren:

```csharp
var people = xprovider.For<Person>()
    .With(p => p.City, "Amsterdam")           // gedeeld door alle 5
    .With("Name", "RandomFirstName()")         // 5x een andere naam
    .BuildMany(5);
```

De tweede overload voegt een per-index `configure` toe die vóór elke `Build()`
wordt toegepast, zodat je de configuratie per (nul-gebaseerde) index kunt laten
variëren terwijl je deze ene builder hergebruikt (de gedeelde basisconfiguratie
blijft behouden). Het is de builder-tegenhanger van de per-index-overload op de
provider (b) hieronder - dezelfde signatuur, maar deze hergebruikt de builder in
plaats van per index een verse te resolven:

```csharp
var people = xprovider.For<Person>()
    .With(p => p.City, "Amsterdam")                              // gedeelde basis, voor allemaal
    .BuildMany(3, (b, i) => b.With(p => p.Name, $"Person{i}"));  // per-index aanpassing
```

**b) Op `IModelBuilderProvider` - elke instance een verse builder:**

```csharp
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count,
    Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName);
IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName,
    Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
```

Elke iteratie krijgt een VERSE builder (uit `provider.For<TModel>()`, of -
met de `modelBuilderName`-vormen - uit `provider.For<TModel>(modelBuilderName)`,
hoofdstuk 5), plus optioneel de (nulgebaseerde) index om per instance te
configureren:

```csharp
var people = xprovider.BuildMany<Person>(5, (b, i) => b
    .With(p => p.Name, $"Person{i}")
    .With(p => p.Address, new Address()));

var dutchPeople = xprovider.BuildMany<Person>(5, "dutch-person", (b, i) => b
    .With(p => p.Name, $"Person{i}"));
```

Wanneer welke vorm? Vorm (a) is de juiste keuze zodra je SOWIESO al een
specifieke builder hebt opgevraagd (bv. via `For<TModel>("naam")` of
`Use<TBuilder>()`) en daarop al configuratie hebt gezet die je voor ALLE
instances wilt delen - dat is met vorm (b) niet uit te drukken, omdat elke
iteratie daar een EIGEN, lege builder krijgt (zie hoofdstuk 18 voor exact
dezelfde afweging bij Gherkin's `CreateModel`/`CreateModels`). Vorm (b) is de
juiste keuze zodra je per instance een AFWIJKENDE, expliciete builder-naam
wilt, of simpelweg helemaal geen gedeelde configuratie nodig hebt.

Op de statische facade (hoofdstuk 14) is vorm (b) beschikbaar als:

```csharp
Create.Models<TModel>(count)                              // == DefaultModelBuilderProvider.Current.BuildMany<TModel>(count)
Create.Models<TModel>(count, modelBuilderName)            // == ...BuildMany<TModel>(count, modelBuilderName)
Create.Models<TModel>(count, configure)                   // == ...BuildMany<TModel>(count, configure)
Create.Models<TModel>(count, modelBuilderName, configure) // == ...BuildMany<TModel>(count, modelBuilderName, configure)
```

### 12.1 Extend: bouwen OP een bestaande instance

`Build()` construeert altijd een NIEUWE instance. `Extend` doet hetzelfde, maar
past de geconfigureerde waarden toe op een MEEGEGEVEN instance in plaats van een
verse te maken:

```csharp
TModel Extend(TModel instance);   // op IModelBuilder<TModel> (kern)
```

Zo bouw je een model op over MEERDERE datasets (bv. meerdere Gherkin-tabellen)
zonder alles in één tabel te wurmen: bouw de basis, en vul die later aan.

```csharp
var order = xprovider.For<Order>().With(o => o.KlantNaam, "Alice").Build(); // basis

xprovider.For<Order>()
    .With(o => o.Betaalwijze, Betaalwijze.OpRekening)
    .Extend(order);   // past dit toe OP order en geeft order terug
```

Eigenschappen (zo intuïtief mogelijk gehouden):

- **Zelfde pipeline als `Build()`**: intern geeft `CreateInstance()` de
  meegegeven instance terug in plaats van een nieuwe te maken, waarna de
  `With`/`WithValues`-waarden er bovenop komen. Een `Build()`-override
  (hoofdstuk 13) draait dus óók, zodat berekende/afgeleide velden herrekend
  worden.
- **One-shot, terminaal**: `Extend` verandert de interne builder-state NIET. Je
  kunt vóór én ná `Extend` gewoon `Build()` aanroepen; elke `Build()` maakt weer
  een verse instance.
- **Alles wat je opgeeft wordt toegepast**, ongeacht setter/init/ctor/backing-
  field. Omdat er geen constructor draait, worden waarden die anders
  constructor-argumenten zouden zijn direct op de bestaande instance gezet (via
  de setter of het backing field). Members die je NIET opgeeft, houden hun
  bestaande waarde.

Voor de Gherkin-integratie is er een handige variant op de provider die één
geneste member uit een eigen tabel bouwt en op de bestaande instance zet - zie
hoofdstuk 18 (`xprovider.Extend(instance, x => x.Adres, table)`).

## 13. Eigen ModelBuilders schrijven

Voor de meeste modeltypen heb je geen eigen builder nodig: de generieke
`DefaultModelBuilder<T>` (geregistreerd als "default"-fallback) werkt direct.
Schrijf een eigen builder wanneer je standaarddefaults wilt vastleggen die
elke keer automatisch worden toegepast, of wanneer je het bouwgedrag wilt
aanpassen.

```csharp
[ModelBuilder]   // optioneel; zonder attribuut heeft de builder geen naam
public sealed class PersonBuilder(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xprovider)
    : ModelBuilder<PersonBuilder, Person>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(x => x.Name, "Onbekend");
        With(x => x.City, "Amsterdam");
    }
}
```

Registreer hem (zodat hij i.p.v. de generieke default wordt gebruikt voor
`Person`):

```csharp
services.AddModelBuilder<PersonBuilder>();
```

`SetDefaults()` wordt aangeroepen vanuit de constructor (via `Reset()`), dus
elke nieuwe builder-instantie - en elke keer dat je `Reset()` aanroept - begint
met deze defaults. Latere `With`-aanroepen overschrijven ze gewoon.

Wil je meerdere varianten van een builder voor hetzelfde modeltype kunnen
registreren en achteraf eenduidig bij naam kunnen opvragen, gebruik dan
`[ModelBuilder("naam")]` - zie hoofdstuk 5.

Je kunt ook `CreateInstance()` of `ApplyDeepPathSetting()` overriden voor meer
geavanceerd gedrag, maar dat is voor de meeste scenario's niet nodig.

**Berekende (cross-field) defaults via een `Build()`-override.** `Build()` is
`virtual`: override hem, roep `base.Build()` aan (dan zijn alle `With`-/tabel-
waarden al toegepast) en bereken afgeleide velden. Gebruik de `protected`-helper
`SetMember(model, x => x.Veld, waarde)` om de waarde te zetten - die gebruikt
dezelfde memberresolutie als de deep-paths (property-setter, init-only, of - bij
geen setter - het backing field), dus ook op read-only/init-only members:

```csharp
public override Product Build()
{
    var product = base.Build();
    if (product.PriceWithVat is null)                         // alleen als niet opgegeven
        SetMember(product, x => x.PriceWithVat, product.Price * 1.21m);
    return product;
}
```

Met een nullable/sentinel-veld onderscheid je "niet opgegeven" van een expliciete
waarde. De enige uitzondering die dit niet dekt is een waarde die ZELF een
constructor-argument is én van een ander constructor-argument afhangt; produceer
die vóór constructie door in plaats hiervan `CreateInstance()` te overriden.

## 14. Statisch gebruik zonder DI-container

Voor scripts, snelle unit tests of contexten zonder DI-container biedt
`XModelBuilder.Default.DefaultModelBuilderProvider.Current` een process-wide
singleton-provider die hetzelfde `IModelBuilderProvider`-contract implementeert.

**Belangrijk architectuurdetail:** dit is GEEN apart, met de hand geschreven
resolutie-algoritme. Vanbinnen houdt `DefaultModelBuilderProvider` een eigen
`ServiceCollection` bij, en wordt - lazy, bij de eerstvolgende aanroep na een
wijziging - een ECHTE `IServiceProvider` gebouwd en gewrapt in dezelfde
`XModelBuilder.DependencyInjection.ModelBuilderProvider` die de DI-integratie
ook gebruikt. Elke `Add*`/`Set*`-aanroep hieronder markeert de interne
`ServiceCollection` "vuil"; de eerstvolgende `For`/`Use`/`Faker`-aanroep herbouwt dan
de `IServiceProvider`. Dit betekent: je kunt op elk moment blijven registreren,
ook NA eerder gebruik, precies zoals voorheen - maar zonder dat er een
tweede, met de hand onderhouden resolutie-implementatie naast de DI-versie
bestaat. Het is dus ook de reden waarom `IServiceProvider`-auto-injectie in
fakers (hoofdstuk 11) ook hier gewoon werkt: er IS nu altijd een echte
`IServiceProvider`, ook zonder dat je er zelf één hebt opgezet.

```csharp
DefaultModelBuilderProvider.Current
    .SetDefaultModelBuilder<MyOpenGenericBuilder>()   // wijzig de open-generic fallback
                                                       // (i.p.v. DefaultModelBuilder<>)
    .AddModelBuilder<PersonBuilder>()                  // registreer een specifieke builder
                                                       // voor Person (mag meerdere keren
                                                       // per modeltype)
    .AddFaker(new PersonFakers())                      // kant-en-klare instance
    .AddFaker<OtherFakers>()                           // of: container construeert hem
    .AddServices(s => s.AddSingleton<SomeDependency>()) // escape hatch: registreer iets
                                                       // willekeurigs (bv. een dependency
                                                       // die een container-gebouwde
                                                       // faker nodig heeft)
    .AddOptions(o => o.DefaultCulture = ...);          // herconfigureer culture
```

Vier statische gemaksklassen liggen hier dun overheen:

```csharp
For.Model<T>()           // == DefaultModelBuilderProvider.Current.For<T>()
Use.Builder<TBuilder>()  // == DefaultModelBuilderProvider.Current.Use<TBuilder>()
Use.Faker<TFaker>()      // == DefaultModelBuilderProvider.Current.Faker<TFaker>()
Create.Model<T>()        // == DefaultModelBuilderProvider.Current.For<T>().Build()
Create.Models<T>(...)    // == DefaultModelBuilderProvider.Current.BuildMany<T>(...) (hoofdstuk 12)
```

`Use` verschilt van `For`: `For<T>()` zoekt een builder op basis van het
MODELTYPE T (volgens de order-onafhankelijke regel uit hoofdstuk 5: één builder,
anders de met `UseAsDefaultModelBuilder` geconfigureerde default, anders de
generieke fallback);
`Use<TBuilder>()` instantieert een SPECIFIEKE, compile-time bekende
builder-klasse rechtstreeks, ongeacht of er voor het bijbehorende modeltype
iets geregistreerd staat. Dit is handig als je voor één modeltype meerdere,
verschillend-genaamde builders hebt (bijvoorbeeld om verschillende
"scenario's" te modelleren) en je in code precies weet welke je wilt.
`Use.Faker<TFaker>()` is hetzelfde idee, maar dan voor fakers (hoofdstuk 11).

## 15. Build-algoritme, instantiatie-fallbacks en randgevallen

**Constructor-selectie** (eenmalig per gesloten `TModel`-type, statisch gecachet):

1. Vraag `typeof(TModel).GetConstructors()` op (ALLEEN PUBLIEKE constructors).
2. Zijn er nul publieke constructors: er is geen "model-constructor";
   het bouwen valt later terug op de Instantiator-fallback (zie
   hieronder). Er wordt GEEN exception gegooid op dit punt.
3. Is er precies één publieke constructor: gebruik die.
4. Zijn er meerdere: kies de constructor met de minste parameters (bij
   gelijke aantallen: de eerste die `GetConstructors()` teruggeeft - geen
   verdere tie-breaking).
5. "Standaard activator"-vlag: waar als de gekozen constructor nul
   parameters heeft, OF als ALLE parameters optioneel zijn (d.w.z. een
   kale `Activator.CreateInstance(typeof(TModel))` is voldoende).

**`CreateInstance()`** (aangeroepen aan het begin van elke `Build()`):

a. Staat de "standaard activator"-vlag: `Activator.CreateInstance(typeof(TModel))`.
b. Anders, is er geen model-constructor gevonden (stap 2 hierboven):
   `Instantiator.CreateInstance(typeof(TModel))` - zie hieronder.
c. Anders: bouw de argumentenlijst voor de gekozen constructor. Voor elke
   parameter: is er een opgeslagen constructor-argument (hoofdstuk 8)?
   Gebruik de value-factory (indien aanwezig), anders de opgeslagen
   waarde (een string wordt hier alsnog via `ValueConverter` naar het
   parameter-type geconverteerd), anders - als er niets is opgeslagen -
   de eigen default-waarde van de parameter, anders null. Roep de
   constructor aan met deze argumenten.

**`Instantiator.CreateInstance(Type)`** - de "maak me ALTIJD een instantie, wat er
ook gebeurt"-fallback, gebruikt door zowel `CreateInstance()` (stap b) als het
`"new()"`-token in ValueConverter:

1. Zoek (reflectie, publiek + niet-publiek, instance) naar een
   PARAMETERLOZE constructor. Bestaat die, roep hem rechtstreeks aan
   (dit werkt ook voor PRIVATE/PROTECTED constructors).
2. Bestaat die niet: kies de constructor (publiek + niet-publiek) met de
   minste parameters.
3. Bouw een argumentenlijst: string-parameters krijgen `""`, value-type-
   parameters krijgen hun default-waarde (via `Activator.CreateInstance`
   op het parameter-type), reference-type-parameters krijgen null.
4. Roep die constructor aan met de gesynthetiseerde argumenten.
5. Gooit die aanroep een exception (bijvoorbeeld door validatielogica in
   de constructor-body), val dan terug op
   `RuntimeHelpers.GetUninitializedObject(modelType)`: een CLR-primitief
   dat een object alloceert ZONDER enige constructor uit te voeren. Dit
   garandeert dat er ALTIJD een instantie wordt geretourneerd, nooit een
   exception.

**`Build()` (volledige volgorde):**

1. `CreateInstance()` (zoals hierboven).
2. Voor elke eerder via `With`/`WithValues` opgegeven deep-path-instelling,
   in de volgorde van opgave: pas hem toe op de net gemaakte instantie
   (hoofdstuk 7).
3. Retourneer de instantie.

**`Reset()`:**
Wist de interne lijst van deep-path-instellingen en de tabel met
constructor-argumenten, en roept `SetDefaults()` opnieuw aan.

## 16. Architectuur / bestandsoverzicht

**Publieke API** (namespace `XModelBuilder` / `XModelBuilder.Default` /
`XModelBuilder.DependencyInjection`):

| Bestand | Type(s) | Omschrijving |
|---|---|---|
| `IModelBuilder.cs` | `IModelBuilder`, `IModelBuilder<TModel>` | builder-contracten |
| `IModelBuilderProvider.cs` | `IModelBuilderProvider` | resolutie-contract |
| `ModelBuilder.cs` | `ModelBuilder<TBuilder,TModel>` | kernimplementatie |
| `ModelBuilderOptions.cs` | `ModelBuilderOptions` | culture-instellingen |
| `ModelBuilderAttribute.cs` | `ModelBuilderAttribute` | verplichte, unieke naam-tag voor builders (hoofdstuk 5) |
| `IFaker.cs` | `IFaker` | marker-interface voor faker-klassen (hoofdstuk 11) |
| `ModelBuilderProviderExtensions.cs` | `BuildMany<TModel>(...)` op `IModelBuilderProvider` | extension methods (hoofdstuk 12) |
| `ModelBuilderExtensions.cs` | `BuildMany<TModel>(...)` op `IModelBuilder<TModel>` | extension method (hoofdstuk 12) |
| `Default/DefaultModelBuilder.cs` | `DefaultModelBuilder<TModel>` | "geen defaults"-builder |
| `Default/DefaultModelBuilderProvider.cs` | `DefaultModelBuilderProvider` | dunne, lazy-ServiceProvider-gebaseerde statische singleton-provider (geen eigen resolutielogica, zie hoofdstuk 14) |
| `Default/For.cs`, `Default/Use.cs`, `Default/Create.cs` | `For`, `Use`, `Create` | statische gemaksfacades |
| `DependencyInjection/ModelBuilderProvider.cs` | `ModelBuilderProvider` | DI-gebaseerde provider; ENIGE plek met echte resolutielogica (ook gebruikt door de standalone provider hierboven) |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddXModelBuilder`, `AddModelBuilder`, `AddModelBuildersFromAssembly`, `AddModelBuildersFromAssemblies`, `AddDefaultModelBuilder`, `UseAsDefaultModelBuilder`, `ValidateXModelBuilderRegistrations`, `AddFaker` | registratie-extensies |
| `DependencyInjection/ModelBuilderDefaults.cs` | `ModelBuilderDefaults` (internal) | order-onafhankelijke registry (modeltype → default-builder), gevuld door `UseAsDefaultModelBuilder`, geraadpleegd door de provider (hoofdstuk 5) |
| `DependencyInjection/XModelBuilderIsolation.cs` | `XModelBuilderIsolation` (enum), `XModelBuilderIsolationState` (internal) | isolatiekeuze (Shared/PerScope) + order-onafhankelijke "laatste verzoent"-bedrading van provider/fakers/seeders (hoofdstuk 21.1) |
| `DependencyInjection/AssemblyScanner.cs` | `AssemblyScanner` (internal) | scant het AppDomain (bin-map laden + cachen, cache-invalidatie bij `AssemblyLoad`) voor `AddModelBuildersFromAssemblies`; degradeert netjes bij niet-laadbare afhankelijkheden (`ReflectionTypeLoadException`/`IsVisible`) |

**Interne hulplogica** (namespace `XModelBuilder.Core`, allemaal `internal`,
behalve `FriendlyNameExtensions` dat publiek is voor foutmeldingen):

| Bestand | Omschrijving |
|---|---|
| `Core/Parser.cs` | statische façade rond `DataParser` |
| `Core/DataParser.cs` | parser voor de mini-datataal (hoofdstuk 9) |
| `Core/CharScanner.cs` | karakter-voor-karakter scanner met foutmeldingen-met-context, gebruikt door `DataParser` |
| `Core/ValueConverter.cs` | alle conversielogica (hoofdstuk 10), inclusief Dictionary-/HashSet-conversie en de faker-tokenherkenning (regex op `"naam(args)"`) |
| `Core/IFakerInvocationSource.cs` | internal-only interface met `InvokeFaker(...)`; BEWUST GEEN lid van de publieke `IModelBuilderProvider` (hoofdstuk 11) - alleen de twee ingebouwde providers implementeren hem, `ValueConverter` doet een type-check (`provider is IFakerInvocationSource`) en gooit een `NotSupportedException` als een eigen `IModelBuilderProvider`-implementatie hem niet heeft en er toch een faker-token gebruikt wordt |
| `Core/FakerInvoker.cs` | overload-resolutie en aanroep van `IFaker`-methoden (hoofdstuk 11): zichtbaarheids- en generic-filtering, Type-/IServiceProvider-auto-injectie. Gedeeld door de DI-provider en `DefaultModelBuilderProvider`, die elk alleen hun eigen lijst geregistreerde `IFaker`-instances en hun eigen `IServiceProvider` aanleveren |
| `Core/StringPathSetter.cs` | past string-deep-paths toe op een object (hoofdstuk 7) |
| `Core/LambdaPathSetter.cs` | past lambda-expressie-deep-paths toe op een object (hoofdstuk 7) |
| `Core/Instantiator.cs` | "altijd een instantie"-fallback (hoofdstuk 15) |
| `Core/HelperExtensions.cs` | reflectie-hulpfuncties: memberresolutie (`TryGetWritableMember`), member get/set, listelement-type-detectie, lijst-vergroting, lambda-padontleding, `ModelBuilderAttribute`-naamresolutie (`GetModelBuilderName`, `HasModelBuilderName`, `GetModelType`), Dictionary-/HashSet-typeargument-detectie (`GetDictionaryTypeArgumentsOrNull`, `GetSetElementTypeOrNull`) |
| `Core/FriendlyNameExtensions.cs` | leesbare typenamen voor foutmeldingen (bv. `"List<Person>"` i.p.v. `"List\`1"`) |

NuGet-afhankelijkheden van het kernproject: `Microsoft.Extensions.DependencyInjection`
(de VOLLEDIGE package, niet alleen `.Abstractions` - nodig omdat
`DefaultModelBuilderProvider` zelf een `ServiceCollection`/`ServiceProvider`
bouwt, niet enkel de interfaces consumeert) en
`Microsoft.Extensions.Options.ConfigurationExtensions`.

Losse integratieprojecten (zie hoofdstuk 18):

| Bestand | Omschrijving |
|---|---|
| `XModelBuilder.Reqnroll/ReqnrollTableExtensions.cs` | Extension methods `CreateModel<T>`/`CreateModels<T>` op `Reqnroll.Table` |
| `XModelBuilder.SpecFlow/SpecFlowTableExtensions.cs` | Dezelfde extension methods op `TechTalk.SpecFlow.Table` |
| `XModelBuilder.Fakers.XFaker/Faker.cs`, `XFakerApi.cs` (+ `ServiceCollectionExtensions.cs`, `ModelBuilderProviderExtensions.cs`) | Dependency-vrije faker: `Faker` stelt zijn deterministische primitieven beschikbaar onder de `XFake`-namespace (methodes op `XFakerApi`, tokens `xfake.*`), plus `AddXFaker(seed)` en de gemaks-accessor `provider.XFaker()` (hoofdstuk 21) |
| `XModelBuilder.Fakers.Bogus/BogusFaker.cs` (+ `ServiceCollectionExtensions.cs`, `ModelBuilderProviderExtensions.cs`) | `BogusFaker` (expose't een geseede Bogus `Faker`), `AddBogusFaker(seed)` en de gemaks-accessor `provider.Bogus()` (hoofdstuk 21) |

## 17. Volledige API-referentie (signatures)

```csharp
public interface IModelBuilder
{
    Type ModelType { get; }
    IModelBuilder Reset();
    IModelBuilder With(LambdaExpression memberPath, object? value);
    IModelBuilder With(LambdaExpression memberPath, Func<object?> valueFactory);
    IModelBuilder With(LambdaExpression memberPath, Func<IModelBuilderProvider, object?> valueFactory);
    IModelBuilder With(string memberPath, string value);
    IModelBuilder WithBuilder(LambdaExpression memberPath, string builderName);
    IModelBuilder WithValues(IEnumerable<KeyValuePair<string, string?>> values);
    object Build();
    object Extend(object instance);   // bouwt op een bestaande instance (hoofdstuk 12.1)
}

public interface IModelBuilder<TModel>
{
    Type ModelType { get; }
    IModelBuilder<TModel> Reset();
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, TValue? value);
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter,
        Func<IModelBuilder<TValue>, IModelBuilder<TValue>> builder) where TValue : class;
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<TValue?> valueFactory);
    IModelBuilder<TModel> With<TValue>(Expression<Func<TModel, TValue>> getter, Func<IModelBuilderProvider, TValue?> valueFactory);
    IModelBuilder<TModel> With(string memberPath, string value);
    IModelBuilder<TModel> WithBuilder<TValue>(Expression<Func<TModel, TValue>> getter, string builderName) where TValue : class;
    IModelBuilder<TModel> WithValues(IEnumerable<KeyValuePair<string, string?>> values);
    TModel Build();
    TModel Extend(TModel instance);   // bouwt op een bestaande instance (hoofdstuk 12.1)
}

public interface IModelBuilderProvider
{
    IModelBuilder For(Type modelType);
    IModelBuilder<TModel> For<TModel>() where TModel : class;
    IModelBuilder For(Type modelType, string name);
    IModelBuilder<TModel> For<TModel>(string name) where TModel : class;
    TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder;
    IModelBuilder Use(Type modelBuilderType);
    // Verse, ingebouwde DefaultModelBuilder<TModel> - langs elke (custom/fallback) registratie heen.
    IModelBuilder<TModel> ForEmpty<TModel>() where TModel : class;
    TFaker Faker<TFaker>() where TFaker : IFaker;
    // GEEN InvokeFaker hier - dat is internal-only plumbing, zie
    // Core/IFakerInvocationSource.cs (hoofdstuk 16).
}

// Marker interface: elke niet-private, niet-generieke instance-methode van een
// geregistreerde implementatie is aanroepbaar als "naam(args)"-token (hoofdstuk 11).
public interface IFaker { }

public static class ModelBuilderProviderExtensions
{
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName) where TModel : class;
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
}

public static class ModelBuilderExtensions
{
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count);
    IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure);
}

public abstract class ModelBuilder<TBuilder, TModel> : IModelBuilder<TModel>, IModelBuilder
    where TModel : class
    where TBuilder : ModelBuilder<TBuilder, TModel>
{
    protected ModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider);
    protected abstract void SetDefaults();
    public virtual TModel Build();
    public TModel Extend(TModel instance);   // hoofdstuk 12.1
    protected virtual TModel CreateInstance();
    protected virtual void ApplyDeepPathSetting(TModel model, DeepPathSetting setting);
    // Zet een member (property-setter / init-only / backing field) op een al gebouwd model;
    // handig in een Build()-override voor berekende defaults (hoofdstuk 13).
    protected void SetMember<TValue>(TModel model, Expression<Func<TModel, TValue>> member, TValue? value);
    // + alle leden van IModelBuilder<TModel> en IModelBuilder, sterk
    //   getypeerd retournerend als TBuilder waar mogelijk.
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModelBuilderAttribute(string name) : Attribute   // name is mandatory + unique per model type
{
    public string Name { get; }
}

public class ModelBuilderOptions
{
    public CultureInfo DefaultCulture { get; set; }  // default: InvariantCulture
    public CultureInfo DateTimeCulture { get; set; } // default: InvariantCulture
}

public enum XModelBuilderIsolation { Shared, PerScope }   // hoofdstuk 21.1

public static class ServiceCollectionExtensions
{
    IServiceCollection AddXModelBuilder(this IServiceCollection services,
        Action<ModelBuilderOptions>? configure = null,
        XModelBuilderIsolation isolation = XModelBuilderIsolation.Shared);
    // Registreert seeder-services waarvan de lifetime de isolatie volgt (order-onafhankelijk);
    // gebruikt door AddXFaker/AddBogusFaker.
    IServiceCollection AddIsolatedXModelBuilderServices(this IServiceCollection services,
        Action<IServiceCollection, ServiceLifetime> register);
    IServiceCollection AddModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection AddModelBuilder<TModelBuilder>(this IServiceCollection services)
        where TModelBuilder : IModelBuilder;
    IServiceCollection AddModelBuildersFromAssembly(this IServiceCollection services, Assembly assembly);
    IServiceCollection AddModelBuildersFromAssemblies(this IServiceCollection services);   // hele AppDomain
    IServiceCollection AddDefaultModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection UseAsDefaultModelBuilder(this IServiceCollection services, Type modelBuilderType);
    IServiceCollection UseAsDefaultModelBuilder<TModelBuilder>(this IServiceCollection services)
        where TModelBuilder : IModelBuilder;
    IServiceCollection ValidateXModelBuilderRegistrations(this IServiceCollection services);
    IServiceCollection AddFaker(this IServiceCollection services, Type fakerType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton);
    IServiceCollection AddFaker<TFaker>(this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker;
}

public sealed class DefaultModelBuilderProvider : IModelBuilderProvider
{
    public static DefaultModelBuilderProvider Current { get; }
    public DefaultModelBuilderProvider SetDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider SetDefaultModelBuilder(Type defaultModelBuilderType);
    public DefaultModelBuilderProvider AddModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider AddModelBuilder(Type modelBuilderType);
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public DefaultModelBuilderProvider UseAsDefaultModelBuilder(Type modelBuilderType);
    public DefaultModelBuilderProvider Validate();   // == ValidateXModelBuilderRegistrations
    public DefaultModelBuilderProvider AddFaker(IFaker faker);
    public DefaultModelBuilderProvider AddFaker<TFaker>(ServiceLifetime lifetime = ServiceLifetime.Singleton) where TFaker : IFaker;
    public DefaultModelBuilderProvider AddServices(Action<IServiceCollection> configure);
    public DefaultModelBuilderProvider AddOptions(Action<ModelBuilderOptions>? configure = null);
    // + For/For<T>/For(Type,string)/For<T>(string)/Use/Use<T>/Faker<T> uit IModelBuilderProvider
}

public static class For   { public static IModelBuilder<TModel> Model<TModel>() where TModel : class; }
public static class Use
{
    public static TModelBuilder Builder<TModelBuilder>() where TModelBuilder : IModelBuilder;
    public static TFaker Faker<TFaker>() where TFaker : IFaker;
}
public static class Create
{
    public static TModel Model<TModel>() where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class;
}

// XModelBuilder.Reqnroll / XModelBuilder.SpecFlow (zie hoofdstuk 18):
public static class ReqnrollTableExtensions   // resp. SpecFlowTableExtensions
{
    // Configureerbare, taalafhankelijke verticale-tabel-kolomnamen (default: EN + NL):
    public static IReadOnlyList<VerticalTableHeader> VerticalTableHeaders { get; }        // read-only
    public static void Configure(Action<ReqnrollTableOptions> configure);                 // resp. SpecFlowTableOptions

    TModel CreateModel<TModel>(this IModelBuilder<TModel> builder, Table table);
    IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table) where TModel : class;
    IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table, string modelBuilderName) where TModel : class;

    IModelBuilder<TModel> WithValue<TModel, TValue>(this IModelBuilder<TModel> builder,
        Expression<Func<TModel, TValue>> member, Table table) where TValue : class;
    TModel Extend<TModel, TValue>(this IModelBuilderProvider provider,
        TModel instance, Expression<Func<TModel, TValue>> member, Table table)
        where TModel : class where TValue : class;
}

public readonly record struct VerticalTableHeader(string FieldColumn, string ValueColumn);

public sealed class ReqnrollTableOptions   // resp. SpecFlowTableOptions
{
    public IList<VerticalTableHeader> VerticalTableHeaders { get; set; }   // geseed met de huidige conventies
}
```

## 18. Gherkin-integratie: Reqnroll en SpecFlow

Voor projecten die Gherkin/BDD-stappen gebruiken, bestaan twee losse
class-library-projecten (elk met hun eigen NuGet-afhankelijkheid, zodat een
project dat alleen Reqnroll gebruikt niet ook SpecFlow hoeft te installeren,
en omgekeerd):

- `XModelBuilder.Reqnroll` → extension methods op `Reqnroll.Table`
- `XModelBuilder.SpecFlow` → extension methods op `TechTalk.SpecFlow.Table`

Beide bieden EXACT dezelfde extension methods (in hun eigen namespace,
`XModelBuilder.Reqnroll` resp. `XModelBuilder.SpecFlow`) - bewust verdeeld over
TWEE verschillende "ankertypes", niet allemaal op `Table`:

```csharp
TModel CreateModel<TModel>(this IModelBuilder<TModel> builder, Table table);

IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table)
    where TModel : class;

IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table, string modelBuilderName)
    where TModel : class;

// Eén GENESTE member uit een EIGEN tabel bouwen (i.p.v. alles in één tabel):
IModelBuilder<TModel> WithValue<TModel, TValue>(this IModelBuilder<TModel> builder,
    Expression<Func<TModel, TValue>> member, Table table) where TValue : class;

// Idem, maar op een BESTAANDE instance (multi-tabel over meerdere stappen):
TModel Extend<TModel, TValue>(this IModelBuilderProvider provider,
    TModel instance, Expression<Func<TModel, TValue>> member, Table table)
    where TModel : class where TValue : class;
```

**`WithValue(member, table)`** zet één member op de waarde van een
`TValue` die uit `table` wordt gebouwd (via de eigen builder van `TValue`), en
gaat verder in de fluent chain. Zo vul je een geneste member uit zijn EIGEN
tabel:

```csharp
var klant = xprovider.For<Klant>()
    .With(k => k.Naam, "Alice")
    .WithValue(k => k.Adres, adresTabel)   // Adres uit een aparte tabel
    .Build();
```

**`Extend(instance, member, table)`** (op de provider) doet hetzelfde maar op een
AL GEBOUWDE instance, en geeft die terug - handig om een model over meerdere
Gherkin-stappen/tabellen samen te stellen:

```csharp
xprovider.Extend(klant, k => k.Adres, adresTabel);   // zet alleen klant.Adres
```

Belangrijk: deze `Extend` past de set toe via een VERS geconstrueerde,
ingebouwde `DefaultModelBuilder<TModel>` (`provider.ForEmpty<TModel>()`) -
NIET via `TModel`'s eigen (custom) builder. Daardoor draaien de `SetDefaults`/
`Build()`-override van die builder NIET mee: er wordt gegarandeerd alléén die
ene member gezet, zonder dat andere velden per ongeluk (opnieuw) worden ingevuld.
De geneste `TValue` (bv. `Adres`) wordt wél met zijn eigen builder gebouwd.

`CreateModel` (enkelvoud) hangt aan een AL OPGEVRAAGDE builder (via
`xprovider.For<TModel>()` of `xprovider.Use<TBuilder>()`, hoofdstuk 4) in plaats van
aan de tabel of de provider. Dat heeft twee redenen:

- Consistentie: alle andere "bouw een model"-aanroepen in XModelBuilder
  hangen al aan de builder/provider (`For`, `Use`, `With`, `Build`, `BuildMany`) -
  niet aan een willekeurige databron. Eén mentaal model: je vraagt eerst een
  builder op, en "voedt" die met data, of dat nu via `With()`, `WithValues()` of
  nu `CreateModel(table)` is.
- Het laat je VOORAF al handmatig configureren en de tabel daar bovenop laten
  toepassen, omdat het dezelfde builder-instantie is die `WithValues(...)` en
  `Build()` aanroept:

```csharp
var person = xprovider.For<Person>()
    .With(p => p.Country, "NL")    // vaste waarde, niet uit de tabel
    .CreateModel(table);            // tabel-waarden overschrijven/vullen de rest
```

`CreateModels` (meervoud) hangt WEL aan de provider, niet aan een builder-
instantie. Dat is geen inconsistentie maar een gevolg van een ander aantal
benodigde builder-instances: een horizontale tabel met N rijen beschrijft N
ONAFHANKELIJKE instances, die elk hun EIGEN `Build()` nodig hebben. Zou je dit
op één builder-instance implementeren door tussen rijen door `Reset()` aan te
roepen, dan zou elke handmatige voorconfiguratie (zoals het Country-
voorbeeld hierboven) na de EERSTE rij verloren gaan, omdat `Reset()` ook die
voorconfiguratie wist, niet alleen de tabel-waarden van de vorige rij. Door
`CreateModels` op de provider te laten staan (net als `BuildMany`, hoofdstuk
12) krijgt elke rij gewoon zijn eigen verse builder via `For<TModel>()`, zonder
die valkuil.

Beide methoden herkennen AUTOMATISCH (intelligent) welke van de twee
gebruikelijke Gherkin-tabelvormen is gebruikt, en zetten die om naar één of
meer aanroepen van `WithValues(...)` - dus alle normale conversieregels
(hoofdstuk 9 en 10, inclusief `null()`/`new()`/`default()`, faker-aanroepen en
named builder references) zijn gewoon van toepassing op de celwaarden.

**Vorm 1 - VERTICAAL ("Field/Value"):** de tabel heeft EXACT twee kolommen, en
de kolomkoppen (hoofdletter-ongevoelig, getrimd) komen overeen met een van de
geconfigureerde conventies. Elke rij beschrijft dan ÉÉN member van ÉÉN instance:

```gherkin
| Field | Value     |
| Name  | John      |
| City  | Amsterdam |
```

**Kolomnamen zijn CONFIGUREERBAAR (taalafhankelijk).** De conventies zijn niet
hardcoded, maar leven in het integratiepackage (dus in de Reqnroll/SpecFlow-laag,
niet in de core - de core kent geen tabellen). Je leest ze via een read-only
property en wijzigt ze via `Configure`:

```csharp
// read-only weergave van de huidige conventies:
public static IReadOnlyList<VerticalTableHeader> ReqnrollTableExtensions.VerticalTableHeaders { get; }
public static IReadOnlyList<VerticalTableHeader> SpecFlowTableExtensions.VerticalTableHeaders { get; }

// wijzigen (proces-breed; het package registreert geen services, dus geen DI-Add):
public static void ReqnrollTableExtensions.Configure(Action<ReqnrollTableOptions> configure);
public static void SpecFlowTableExtensions.Configure(Action<SpecFlowTableOptions> configure);

public readonly record struct VerticalTableHeader(string FieldColumn, string ValueColumn);
```

Standaard bevatten ze Engels én Nederlands: (`"field"`,`"value"`), (`"key"`,`"value"`),
(`"name"`,`"value"`), (`"property"`,`"value"`), (`"veld"`,`"waarde"`),
(`"eigenschap"`,`"waarde"`), (`"sleutel"`,`"waarde"`). Roep `Configure` typisch één
keer aan bij test-run-start; de options zijn geseed met de huidige conventies, dus
je kunt toevoegen of de lijst helemaal vervangen:

```csharp
// een taal toevoegen:
ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders.Add(new("champ", "valeur")));

// of helemaal vervangen:
ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders =
[
    new("champ", "valeur"),
]);
```

Zo werkt in een Nederlandstalig feature-bestand ook `| Veld | Waarde |` gewoon:

```gherkin
| Veld | Waarde    |
| Name | John      |
| City | Amsterdam |
```

**Vorm 2 - HORIZONTAAL:** elke andere tabelvorm (ook een tabel met toevallig
twee kolommen die NIET aan de bovenstaande naamconventie voldoen, zoals een
entiteit met precies twee eigenschappen). De kolomkoppen zijn dan de
membernamen, en elke datarij beschrijft ÉÉN instance:

```gherkin
| Name | City      |
| John | Amsterdam |
| Jane | Utrecht   |
```

**`CreateModel(builder, table)`:**

- Verticale tabel: bouwt altijd precies één instance (alle rijen samen).
- Horizontale tabel met EXACT één datarij: bouwt die ene instance.
- Horizontale tabel met 0 of meer dan 1 datarij: gooit een
  `InvalidOperationException` ("gebruik `CreateModels<T>()` op de provider
  voor een lijst").

**`CreateModels<TModel>(provider, table)`:**

- Verticale tabel: retourneert een lijst met PRECIES ÉÉN element (een
  verticale tabel kan per definitie maar één instance beschrijven).
- Horizontale tabel: retourneert één instance per datarij, in
  tabelvolgorde, elk via een eigen, verse builder
  (`provider.For<TModel>()` - dus volgens de resolutie van hoofdstuk 5: de
  enige builder, anders de met `UseAsDefaultModelBuilder` geconfigureerde
  default, anders de generieke fallback).

**`CreateModels<TModel>(provider, table, modelBuilderName)`:**

Zelfde als hierboven, maar elke rij gebruikt EXPLICIET de builder die
geregistreerd staat onder `[ModelBuilder(modelBuilderName)]` voor
`TModel` (via `provider.For<TModel>(modelBuilderName)`, hoofdstuk 5) -
ongeacht welke builder normaal als "default" zou gelden. Bestaat die
naam niet, dan gooit AL DE EERSTE rij een `KeyNotFoundException`.

Voorbeeld (Reqnroll-stap):

```csharp
using XModelBuilder.Reqnroll;

[Given("the following person")]
public void GivenTheFollowingPerson(Table table)
{
    var person = _xprovider.For<Person>().CreateModel(table);
    // of, voor een specifiek geregistreerde builder:
    var person2 = _xprovider.Use<PersonBuilder>().CreateModel(table);
}

[Given("the following people")]
public void GivenTheFollowingPeople(Table table)
{
    var people = _xprovider.CreateModels<Person>(table);
    // of, voor een specifiek genaamde builder, toegepast op ELKE rij:
    var dutchPeople = _xprovider.CreateModels<Person>(table, "dutch-person");
}
```

Voor SpecFlow is dit identiek, alleen met `using XModelBuilder.SpecFlow;` en
een stap-parameter van het type `TechTalk.SpecFlow.Table` in plaats van
`Reqnroll.Table`.

Implementatiedetail: beide `Table`-klassen (Reqnroll en SpecFlow) hebben
identieke vorm - `Header` (`ICollection<string>`), `Rows` (`IEnumerable` van een
`TableRow`/`DataTableRow` die `IDictionary<string,string>` implementeert, met
zowel een string- als een int-indexer). De twee extension-bestanden zijn
daardoor structureel identiek; alleen de using-namespace verschilt.

## 19. Bekende beperkingen

- Constructor-selectie kijkt alleen naar PUBLIEKE constructors voor het
  "model-constructor"-pad (hoofdstuk 15); de `Instantiator`-fallback kijkt wel
  naar niet-publieke constructors, maar dan zonder enige
  constructor-argumentbinding via `With(...)` (alle argumenten worden met
  type-defaults gevuld).
- Een deep-path die toevallig met een constructor-parameternaam begint maar
  een punt bevat (bv. `"Adres.Straat"` met constructor-parameter `"address"`)
  wordt NIET als constructor-argument herkend; alleen het EXACTE, punt-vrije
  pad (`"Adres"`) wordt zo herkend. Zie hoofdstuk 8.
- Lambda-padindexering ondersteunt alleen een ENKEL, CONSTANT, GEHEEL
  indexargument; berekende of variabele indices, en meervoudige
  indexer-argumenten, worden niet ondersteund (`NotSupportedException`).
- `ValueConverter.AddKnownTypeConverter` werkt PROCESBREED/statisch: het is
  niet gebonden aan één `IModelBuilderProvider`-instantie of aan
  `ModelBuilderOptions`.
- `GetListElementType` (gebruikt om het elementtype van een collectie-member
  te bepalen) herkent arrays, `List<T>`/`IList<T>` en interfaces die `IList<T>`
  implementeren; voor andere collectietypes (bv. een custom `ICollection<T>`
  zonder `IList<T>`) valt het element-type terug op `object`, wat tot
  onverwachte boxing/conversiefouten kan leiden.
- Top-level bare (haakjesloze) array-syntax (`"1,2,3"`) wordt alleen
  ondersteund op het allerhoogste niveau van een conversie; genest binnen
  een array of object zijn vierkante haken altijd verplicht.
- De named-builder-reference-syntax (hoofdstuk 5/10) is alleen van
  toepassing op niet-string referentietypes; voor value-types, `string` en
  `object` werkt elke niet-token tekst als gewone data (consistent met vóór
  deze functionaliteit).
- De "verticaal vs. horizontaal"-detectie van Gherkin-tabellen (hoofdstuk 18)
  is INHERENT ambigu voor een tabel met exact twee kolommen: XModelBuilder
  kiest op basis van de kolomkop-NAMEN (de geconfigureerde Field/Value-achtige
  conventies in `ReqnrollTableExtensions`/`SpecFlowTableExtensions.VerticalTableHeaders`),
  niet op basis van het aantal rijen. Een entiteit met precies twee properties
  waarvan de kolomkoppen toevallig een verticale conventie zijn (bv.
  `"Field"`/`"Value"` of `"Veld"`/`"Waarde"`), wordt dus ten onrechte als
  verticale tabel geïnterpreteerd; gebruik in dat (zeldzame) geval andere
  kolomnamen of een derde kolom.
- Tuples (`Tuple<...>`/`ValueTuple<...>`) worden NIET ondersteund in de mini-taal
  (hoofdstuk 9) - dit is een bewust uitgestelde, optionele uitbreiding.
- De faker-tokensyntax `"naam(args)"` (hoofdstuk 11) reserveert dat hele
  patroon (identifier direct gevolgd door haakjes, eindigend op `')'`) in de
  GEHELE mini-taal, voor ELK doeltype - ook als er geen enkele `IFaker`
  geregistreerd is. Toevallige plain-text data in die exacte vorm (zonder
  spatie, bv. `"Janssen(Junior)"`) wordt dus als faker-aanroep geïnterpreteerd
  en gooit een `KeyNotFoundException` als er geen faker met die naam bestaat;
  gebruik in dat geval het `'@'`-escape-mechanisme.
- `IFaker`-methode-overloads worden gekozen op basis van het AANTAL argumenten
  (tussen het verplichte en totale aantal data-parameters, zodat optionele
  parameters mogen worden weggelaten) plus of elk argument naar het parametertype
  convergeert. Van de passende overloads wint exacte ariteit, anders de minste in
  te vullen defaults - dit is geen volledige "beste match" zoals de C#-compiler
  (geen impliciete numerieke promoties e.d.).
- Deep-path faker-tokens (hoofdstuk 11) lossen het laatste segment op als één
  member: een methode, of - bij geen methode en geen argumenten - een property/
  field. Een methode-dan-property als laatste stap (bv. `x.currency().code`) is
  daardoor NIET als token uit te drukken; gebruik daarvoor de getypeerde route.
- `ModelBuilderProviderExtensions.BuildMany` (op de provider) bouwt elke
  instance via een VERSE builder (`provider.For<TModel>()`, eventueel met
  naam); deze vorm deelt dus NOOIT voorconfiguratie tussen instances. Wil je
  dat wél, gebruik dan de `BuildMany`-variant op `IModelBuilder<TModel>` zelf
  (hoofdstuk 12), die expliciet DEZELFDE builder hergebruikt.
- Faker-zichtbaarheid (hoofdstuk 11) is via reflectie afgedwongen voor de
  TOKEN-route (Public|NonPublic, exclusief private en generic). Voor de
  GETYPEERDE route (`Faker<TFaker>()`/constructor-injectie) gelden de gewone
  C#-toegankelijkheidsregels van de TAAL zelf - een protected/private member
  is dan al niet aanroepbaar vanaf een externe call-site, zonder dat
  XModelBuilder daar zelf iets voor hoeft te doen of te controleren.

## 20. Specificatie-samenvatting (voor het naprogrammeren van dit framework)

Wie dit framework opnieuw wil implementeren (in dezelfde of een andere taal),
heeft in essentie deze bouwstenen nodig, in deze afhankelijkheidsvolgorde:

1. Een character-scanner met `Peek`/`Next`/`Expect`/`SkipWhitespace`/`EOF`-semantiek
   en foutmeldingen die de positie en een tekstfragment rond de fout
   tonen (`CharScanner`).

2. Een recursive-descent parser boven op (1) die de grammatica uit
   hoofdstuk 9 implementeert en als resultaat een boom van
   `string | object[] | Dictionary<string,object>` teruggeeft, met een
   publieke ingang voor "parse top-level array, haakjes optioneel"
   (`DataParser`/`Parser`).

3. Een reflectie-hulplaag (`HelperExtensions`) die:
   - voor een `(Type, naam)` een schrijfbare member vindt volgens de
     regels in hoofdstuk 7 (property-met-setter, dan drie
     backing-field-patronen),
   - het elementtype van een array/lijst/IList-achtig type bepaalt,
   - een lijst tot een gegeven lengte aanvult met defaults of
     providergebouwde elementen,
   - get/set op een `MemberInfo` uniform aanbiedt (property of field),
   - uit een lambda-expressie de "ondiepe" propertynaam haalt (voor
     constructor-argumentdetectie),
   - een builder-klasse koppelt aan zijn (optionele) naam-attribuut
     (voor "is dit de default-builder?" / "heeft deze builder naam X?").

4. Een "maak me altijd een instantie"-routine (`Instantiator`) die eerst een
   parameterloze constructor zoekt (ook niet-publieke), anders de
   constructor met de minste parameters kiest en met type-defaults vult,
   en bij falen teruggrijpt op een manier om een object te alloceren
   zonder constructor te draaien (in .NET: `RuntimeHelpers.GetUninitializedObject`).

5. Een naam-attribuut (zoals `ModelBuilderAttribute`) waarmee een builder-
   klasse een VERPLICHTE, per-modeltype UNIEKE naam krijgt, plus een
   order-onafhankelijke "wijs de default aan"-stap (zoals
   `UseAsDefaultModelBuilder`) en een validatie die uniciteit en het bestaan
   van een default (bij ≥2 builders) afdwingt, gebruikt door (10) om bij
   meerdere geregistreerde builders voor hetzelfde modeltype te bepalen welke
   "de" builder is, en om expliciete naam-gebaseerde lookups te ondersteunen.

6. Een waarde-converteerder (`ValueConverter`) die de algoritme-stappen uit
   hoofdstuk 10 implementeert: drie tokens (`null()`/`new()`/`default()`) plus
   hun escape-mechanisme (één leidend `'@'`-teken), een regex-herkenning van
   het `"naam(args)"`-faker-aanroep-patroon, named-builder-references voor
   complexe types, array/lijst-conversie (met recursie via (2) en
   zichzelf, inclusief `HashSet<T>`/`ISet<T>` als alternatieve doel-vorm),
   `Dictionary<,>`/`IDictionary<,>`-conversie vanuit de object-literal-syntax,
   en object-literal-conversie (bouw een lege instantie via de
   builder-provider, vul members op basis van (3), recursief).

7. Een overload-resolutie-routine (`FakerInvoker`) die, gegeven een lijst
   geregistreerde "faker"-instances (instances van klassen die een lege
   marker-interface implementeren), een naam en ruwe, nog-niet-geconverteerde
   argumenten, plus de `IServiceProvider` van de aanroepende provider: van
   ACHTER NAAR VOREN door de lijst zoekt naar de eerste instance met een
   niet-private, niet-generieke methode van die naam (hoofdletter-
   ongevoelig; die instance "wint" volledig - geen vermenging van
   overloads tussen instances), daarbinnen de eerste overload kiest
   waarvan het aantal parameters (eventuele LEIDENDE parameters van het
   type `System.Type` en/of `IServiceProvider`, in willekeurige onderlinge
   volgorde, niet meegerekend - die krijgen automatisch het doeltype resp.
   de `IServiceProvider`) overeenkomt en waarvan elk argument succesvol naar
   het parametertype converteert via (6), de methode aanroept, en het
   resultaat zo nodig terugconverteert naar het uiteindelijke doeltype.
   Bied daarnaast een GETYPEERDE tegenhanger (`Faker<TFaker>()`) die simpelweg
   de geregistreerde `TFaker`-instance teruggeeft (geen reflectie nodig -
   gewone DI-resolutie/directe instantie), zodat dezelfde fakers ook
   met volledige compile-time-controle aanroepbaar zijn; toegankelijkheid
   van INDIVIDUELE methoden wordt voor deze route door de taal zelf
   afgedwongen (een private/protected methode is vanaf een externe
   call-site al niet aanroepbaar).

8. Twee "deep-path"-toepassers die, gegeven een doel-object en een pad
   (string met dot/bracket-notatie, of een expressie-boom), member voor
   member afdalen volgens de regels in hoofdstuk 7, daarbij gebruikmakend
   van (3) en (6). Bied bij de lambda-variant ook een vorm aan waarbij de
   value-factory de actieve provider als argument krijgt (in plaats van
   die uit een omsluitende scope te moeten sluiten), voor correcte
   herbruikbare factory-functies onder scoped/parallelle providers.

9. Een generieke builder-basisklasse die:
   - bij eerste gebruik per modeltype de constructor selecteert
     (hoofdstuk 15),
   - `With`-aanroepen routeert naar constructor-argumentopslag of naar een
     deep-path-instellingenlijst (hoofdstuk 8), met een apart
     `"WithBuilder"`-pad (lambda + naam) om dezelfde ambiguïteit te
     vermijden die een generieke `With(getter,string)`-overload zou geven
     zodra het membertype zelf `string` is. Constructor-argumentwaarden
     die strings zijn, worden NIET in-place vervangen door hun
     geconverteerde resultaat (cachen zou hertokenisatie/randomisatie bij
     herhaald `Build()` onmogelijk maken) - converteer ze bij elke
     opvraging opnieuw, ongeacht of het parametertype zelf `string` is,
   - bij `Build()` eerst een instantie maakt (via standaard-activator,
     via de geselecteerde constructor met opgehaalde argumenten, of via
     (4) als er geen bruikbare constructor is), en daarna alle
     deep-path-instellingen toepast via (8),
   - een "bouw er N keer, op DEZELFDE builder-instance"-gemaksmethode
     biedt (`BuildMany`) die simpelweg `Build()` N keer aanroept - waarde-
     factories en string-pad-tokens worden dan vanzelf N keer opnieuw
     geëvalueerd, literale waarden blijven gedeeld.

10. Een providerlaag die, gegeven een modeltype (en optioneel een naam),
    een bijbehorende builder teruggeeft, met ondersteuning voor MEERDERE
    geregistreerde builders per modeltype, ORDER-ONAFHANKELIJK: bij precies
    één builder die ene; bij meerdere de via (5) geconfigureerde default (en
    een duidelijke fout als die ontbreekt - geen "laatste wint"); en bij geen
    enkele een generieke fallback-builder (een open generic
    `"DefaultBuilder<T>"` zonder eigen defaults). Dezelfde laag
    beheert ook de lijst geregistreerde fakers voor (7), exposeert die
    faker-aanroep-mogelijkheid via een INTERN-only interface (NIET op het
    publieke provider-contract zelf, om dat strak te houden - de
    waarde-converteerder uit (6) doet een runtime-typecheck tegen die
    interne interface en valt netjes terug op een duidelijke fout als een
    alternatieve providerimplementatie hem niet aanbiedt), en biedt een
    "bouw er N van, elk met een verse builder, optioneel met een specifieke
    naam en/of een per-index-configuratiefunctie"-gemaksmethode (`BuildMany`
    op de provider, te onderscheiden van `BuildMany` op de builder uit (9)).
    Bied zowel een DI-integratie (gebaseerd op "alle geregistreerde
    implementaties voor een service-type opvragen", bv. .NET's
    `GetServices`) als een DI-vrije, statische singleton-variant aan - bij
    voorkeur door de laatste simpelweg een eigen, lazy-(her)opgebouwde
    container te laten beheren en ALLE resolutielogica te laten
    delegeren aan dezelfde DI-implementatie, in plaats van een tweede,
    losstaande resolutie-implementatie te onderhouden - plus een methode
    om EXPLICIET op naam te resolven (met een duidelijke fout bij een
    onbekende naam, zowel voor builders als voor fakers).

11. Eén of meer dunne integratielagen die een framework-specifieke
    "tabel"-representatie (kolomkoppen + rijen van string-waarden)
    normaliseren naar een of meer rijen van naam/waarde-paren, en die
    voeden aan (9)'s `WithValues`-mechanisme - met een heuristiek die
    onderscheid maakt tussen een verticale "veld/waarde"-tabel (kolomkop-
    NAMEN matchen een bekende conventie) en een horizontale tabel
    (kolomkoppen = veldnamen, één rij per instance). Verdeel de "bouw één
    instance" (hangt aan een AL OPGEVRAAGDE builder, voor consistentie en
    om voorconfiguratie te kunnen delen) en "bouw een lijst" (hangt aan de
    PROVIDER, want heeft per rij een eigen, verse builder nodig) bewust
    over twee verschillende ankertypes - dezelfde afweging als bij (9)/(10).

Door deze elf bouwstenen in deze volgorde te implementeren en te testen
(bij voorkeur met de testgevallen die in dit document als voorbeeld zijn
gegeven: constructor-only properties, init-only properties, private
backing fields, array/lijst-indexering, geneste object-literals, tokens,
faker-aanroepen met en zonder Type-/IServiceProvider-auto-injectie en
overloading, faker-zichtbaarheidsregels, getypeerde faker-aanroepen,
Dictionary/HashSet-conversie, meerdere builders per modeltype met
naam-resolutie, BuildMany op zowel builder als provider, culture-specifieke
parsing, en beide Gherkin-tabelvormen) ontstaat een functioneel equivalent
van XModelBuilder inclusief zijn Gherkin-integraties.

## 21. Deterministisch genereren met een seed (XFaker en BogusFaker)

XModelBuilder zelf is volledig deterministisch: bij gelijke `With`-aanroepen
produceert `Build()` altijd hetzelfde model. De ENIGE bron van willekeur zijn je
`IFaker`-methoden (hoofdstuk 11). "Deterministisch genereren met een seed" komt
dus neer op: de RNG binnen je fakers seeden. Daarvoor zijn er twee losse,
opt-in pakketten - houd de kernlibrary dependency-vrij, net als bij Reqnroll/SpecFlow.

### 21.1 De isolatiegrens kies je met `XModelBuilderIsolation`

De provider, de fakers en hun geseede RNG's vormen samen de gedeelde,
stateful kern. Hoe geïsoleerd die is, bepaal je in ÉÉN plek - op
`AddXModelBuilder` - met `XModelBuilderIsolation`:

```csharp
public enum XModelBuilderIsolation { Shared, PerScope }
```

- **`Shared`** (default): één gedeelde provider + fakers + geseede RNG's voor de
  hele container (Singleton). De DI-scope is NIET de grens; bouw voor
  deterministische tests een VERSE `ServiceProvider` per test. Twee providers met
  dezelfde seed reproduceren elkaar exact; tellers beginnen per provider opnieuw.
  Overal veilig te injecteren.
- **`PerScope`**: een verse provider + fakers + geseede RNG's PER DI-scope
  (Scoped). De scope IS de grens: elke scope reseedt, dus een BDD-scenario per
  scope is reproduceerbaar én parallel-veilig. Resolve binnen een scope; injecteer
  de provider niet in een singleton (captive dependency).

```csharp
services.AddXModelBuilder(isolation: XModelBuilderIsolation.PerScope)
        .AddXFaker(seed: 123)
        .AddBogusFaker(seed: 123);

using var scope = root.CreateScope();
var xprovider = scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>();
// alles in deze scope deelt één geseede set; de volgende scope krijgt een verse.
```

De keuze is een ENKELE knop die provider én seeders tegelijk zet, zodat de
kapotte combinatie (scoped faker + singleton provider) niet bestaat. En het is
ORDER-ONAFHANKELIJK: `AddXFaker`/`AddBogusFaker` vóór óf na `AddXModelBuilder`
levert hetzelfde op (registraties die te vroeg komen worden uitgesteld en met de
juiste lifetime geflushed zodra de isolatie bekend is). `ValidateXModelBuilderRegistrations()`
gooit als de provider-lifetime niet bij de isolatie past (bv. door
`AddXModelBuilder` tweemaal met verschillende isolatie aan te roepen).

> Alléén provider + fakers + geseede RNG's volgen de isolatie. `ModelBuilderOptions`,
> de `ModelBuilderDefaults`-registry en de builder-registraties blijven
> container-breed (de `TimeProvider` blijft Singleton).

### 21.2 XModelBuilder.Fakers.XFaker - Faker (dependency-vrij)

Het project `XModelBuilder.Fakers.XFaker` bevat de klasse `Faker` (namespace
`XModelBuilder.Fakers.XFaker`): een kleine, dependency-vrije faker met
deterministische primitieven die Bogus juist NIET goed doet: identiteit
(tellers), volgorde-onafhankelijke naam-GUIDs en klok-gebonden leeftijden. Hij
krijgt een geseede `Random` en een `TimeProvider` via de constructor.

Conform de faker-**namespace-conventie** (hoofdstuk 11) stelt `Faker` zijn hele
methode-oppervlak beschikbaar onder één namespace-member, `XFake` (van het type
`XFakerApi`). Zijn tokens worden dus aangeroepen als `xfake.<methode>()` en NIET op
het top-niveau - net zoals Bogus alles onder `bogus.` aanbiedt (hoofdstuk 21.3).
Zo botsen XFaker's tokens niet met die van andere fakers.

```csharp
using XModelBuilder.Fakers.XFaker;

services.AddXModelBuilder()
    .AddXFaker(seed: 12345);   // registreert Faker + geseede Random (volgt de isolatie, hoofdstuk 21.1)
```

Getypeerd opvragen kan via `xprovider.Faker<Faker>()`, of korter via de
gemaks-accessor `xprovider.XFaker()` (extension op `IModelBuilderProvider`); in
beide gevallen leven de methodes onder `.XFake`:

```csharp
var id = xprovider.XFaker().XFake.NewGuid("customer-acme");
```

| Token / methode | Soort | Toelichting |
|---|---|---|
| `xfake.NextId()` / `xfake.NextId(naam)` | monotone teller(s), start bij 1 | uniek en leesbaar; named counters zijn onderling onafhankelijk |
| `xfake.Sequence("INV-{0:0000}")` | leesbare reeks (INV-0001, ...) | composiet-formaat met een teller per format-string |
| `xfake.NewGuid()` | seeded-random v4-GUID | reproduceerbaar bij gelijke seed + call-volgorde |
| `xfake.NewGuid(naam)` | naam-gebaseerde stabiele GUID (MD5) | zelfde sleutel → zelfde GUID, ONGEACHT volgorde/parallellisme |
| `xfake.IntBetween(min,max)` | seeded int (inclusief) | basisprimitief |
| `xfake.Bool(truePercent)` | seeded boolean | true in ~`truePercent`% van de gevallen |
| `xfake.DateBetween(min,max)` | seeded datum in range | inclusief |
| `xfake.AgeBetween(min,max)` / `xfake.AgeBetween(min,max,atDate)` | geboortedatum voor leeftijdsrange | "nu" komt uit `TimeProvider`, NIET `DateTime.Today` - dus ook deterministisch |

Twee soorten "deterministisch", bewust naast elkaar:

- RNG-gebaseerd (`xfake.NewGuid()`, `xfake.IntBetween`, `xfake.DateBetween`, `xfake.AgeBetween`):
  reproduceerbaar bij een seed, maar de waarde hangt af van hoe vaak de RNG
  al getrokken is (call-volgorde).
- Naam-gebaseerd (`xfake.NewGuid(naam)`): dezelfde sleutel mapt altijd op dezelfde
  GUID, los van volgorde of parallellisme. Te verkiezen wanneer je een STABIELE
  id voor een bekende entiteit wilt in plaats van "zomaar een willekeurige id".

```csharp
var person = xprovider.For<Person>()
    .With("Id", "xfake.NewGuid(customer-acme)")   // stabiel per sleutel
    .With("Birthday", "xfake.AgeBetween(20,30)")  // reproduceerbaar bij seed
    .Build();
```

### 21.3 XModelBuilder.Fakers.Bogus - BogusFaker

Het project `XModelBuilder.Fakers.Bogus` bevat `BogusFaker` (namespace
`XModelBuilder.Fakers.Bogus`), bewust minimaal: het expose't enkel de geseede
Bogus `Faker` als property `Bogus`. De hele Bogus-oppervlakte is bereikbaar via
**deep-path faker-resolutie** (hoofdstuk 11) - er zijn GEEN handgeschreven
adapter-methoden.

```csharp
using XModelBuilder.Fakers.Bogus;

services.AddXModelBuilder()
    .AddBogusFaker(seed: 12345);   // registreert BogusFaker + per-instance geseede Bogus.Faker
```

Vanuit tokens gebruik je een member-pad dat begint bij de `Bogus`-property:

```csharp
.With("Name",  "bogus.name.firstname()")
.With("Email", "bogus.internet.email()")
.With("City",  "bogus.address.city()")
.With("Name",  "bogus.person.firstname()")   // terminal is een property -> via fallback gelezen
```

Het `bogus.`-pad geeft elke generator meteen een namespace, dus deze tokens
botsen niet met je eigen fakers of met de `Faker`-faker. Voor de combinaties die
deep-path niet dekt (bv. methode-dan-property zoals `Finance.Currency().Code`)
gebruik je de getypeerde route - via `Faker<BogusFaker>().Bogus` of de
gemaks-accessor `xprovider.Bogus()` (extension op `IModelBuilderProvider` die de
onderliggende Bogus `Faker` teruggeeft):

```csharp
var county   = xprovider.Faker<BogusFaker>().Bogus.Address.County();
// of je kunt in plaats van xprovider.Faker<BogusFaker>().Bogus ook de short hand extension xprovider.Bogus() gebruiken:
var currency = xprovider.Bogus().Finance.Currency().Code;
```

Bogus gebruikt een EIGEN randomizer (los van `System.Random`). `AddBogusFaker`
seedt die per instance via `new Faker { Random = new Randomizer(seed) }` - NIET
de globale static `Randomizer.Seed`, want die is processbreed en zou parallelle
runs in elkaar laten lopen.

### 21.4 Samen gebruiken

Beide fakers kunnen naast elkaar in dezelfde provider; dankzij het `bogus.`-pad
botsen hun tokens niet:

```csharp
var xprovider = new ServiceCollection()
    .AddXModelBuilder()
    .AddXFaker(seed: 2024)
    .AddBogusFaker(seed: 2024)
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();

var person = xprovider.For<Person>()
    .With("Id", "xfake.NewGuid(customer-acme)")      // Faker (stabiel)
    .With("Name", "bogus.name.firstname()")     // BogusFaker, deep-path
    .With("City", "bogus.address.city()")       // BogusFaker, deep-path
    .Build();
```

### 21.5 Aandachtspunten

- **Verban ander ambient non-determinisme uit je eigen fakers.** Niet alleen
  `Random.Shared`, maar ook `Guid.NewGuid()`, `DateTime.Now`/`UtcNow`. Route
  alles door een geïnjecteerde, geseede `Random` en (voor tijd) een
  `TimeProvider`. Eén ontsnapte `Guid.NewGuid()` maakt het geheel
  non-deterministisch.
- **Gereserveerde tekens in token-argumenten.** Faker-argumenten gaan door de
  mini-taal-parser (hoofdstuk 9), dus tekens als `:`, `,`, `[`, `]`, `{`, `}`
  kunnen niet zomaar in een bare argument staan. Voor een naam-sleutel met zo'n
  teken gebruik je een scheidingsteken als `-` (`NewGuid(customer-acme)`) of een
  string-literal (`NewGuid("customer:acme")`).
- **Gebruik bij value-factories de provider-vorm** (hoofdstuk 6, vorm g) in
  scenario's met meerdere providers, zodat de factory gegarandeerd de juiste
  provider - en dus de juiste geseede faker - krijgt:
  `.With(x => x.Address, p => p.Faker<AddressFakers>().Random())`.
