# Scenario 04 — A deterministic Dutch dataset

**Goal.** Generate a set of Netherlands-specific records — people with a **BSN**, companies with a
**KvK number, BTW/VAT number and IBAN**, addresses with a real-shaped **postcode**, vehicles with a
**kenteken** — where every identifier that carries an official check is actually **valid**, and the
whole set is **reproducible** from a seed.

**Why the identifiers are valid, not just random digits.** `DutchFaker` builds each value through
the reusable checksum helpers in the core package (README chapter 21.4): a BSN/RSIN is mod-11
("elfproef") over random digits, an IBAN is mod-97, an EAN is a GS1 check digit. So the data passes
the same validators your production code uses. Everything is fictitious — only meant as test data.

```csharp
var xprovider = new ServiceCollection()
    .AddXModelBuilder()
    .AddBogusFaker(seed: 2026)   // names, cities, street names
    .AddDutchFaker(seed: 2026)   // nl.* generators (own seeded Random, independent of the others)
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();
```

## The domain

```csharp
public sealed class DutchPerson
{
    public string Name { get; set; } = "";
    public string Bsn { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string Mobile { get; set; } = "";
}

public sealed class DutchCompany
{
    public string Name { get; set; } = "";
    public string KvkNumber { get; set; } = "";
    public string VatNumber { get; set; } = "";   // BTW-nummer, NL{9}B{2}
    public string Iban { get; set; } = "";
}
```

## Build the dataset

You can reach the generators two ways — as **string tokens** (great for tables/Gherkin) or **typed**
via `xprovider.NL()`. Both draw from the same seeded `Random`.

**Typed** (compile-time checked, IntelliSense):

```csharp
var nl = xprovider.NL();

var people = xprovider.BuildMany<DutchPerson>(50, (b, i) => b
    .With(p => p.Name,     xprovider.Bogus().Name.FullName())
    .With(p => p.Bsn,      nl.Bsn())         // valid elfproef
    .With(p => p.Postcode, nl.Postcode())    // "1234 AB" shape, avoids invalid combos
    .With(p => p.Mobile,   nl.Mobiel()));    // 06 + 8 digits

var companies = xprovider.BuildMany<DutchCompany>(20, (b, i) => b
    .With(c => c.Name,      xprovider.Bogus().Company.CompanyName())
    .With(c => c.KvkNumber, nl.KvkNummer())  // 8 digits
    .With(c => c.VatNumber, nl.BtwNummer())  // NL{9}B{2}, elfproef on the 9-digit core
    .With(c => c.Iban,      nl.Iban()));      // valid ISO 13616 mod-97
```

**As tokens** — identical result, addressed through the `nl.` namespace:

```csharp
var person = xprovider.For<DutchPerson>()
    .With("Bsn",      "nl.Bsn()")
    .With("Postcode", "nl.Postcode()")
    .With("Mobile",   "nl.Mobiel()")
    .Build();
```

The token form is what makes these values usable straight from a Reqnroll/SpecFlow table
(`| Bsn | nl.Bsn() |`) with no glue code — see README chapter 18.

## Work with it further — prove validity and reproducibility

The core `Checksums` helpers (public in the `XModelBuilder` package) let you *re-verify* the
generated identifiers with the exact algorithm behind each check — useful as a sanity assertion, or
to validate identifiers coming from elsewhere in your own code:

```csharp
using XModelBuilder;   // Checksums

// BSN: mod-11 with the official weights 9,8,7,6,5,4,3,2,-1
int[] bsnWeights = [9, 8, 7, 6, 5, 4, 3, 2, -1];
Assert.All(people, p => Assert.True(Checksums.Mod11IsValid(p.Bsn, bsnWeights)));

// IBAN: rearrange (first 4 chars to the end) then mod-97 must equal 1
static bool IbanIsValid(string iban) =>
    Checksums.Mod97(iban[4..] + iban[..4]) == 1;

Assert.All(companies, c => Assert.True(IbanIsValid(c.Iban)));
```

**Reproducibility.** A second provider with the *same* seed produces the *same* set, in the same
order — so you can snapshot the dataset to a golden file, or compare two runs:

```csharp
List<string> BsnsWithSeed(int seed)
{
    var p = new ServiceCollection()
        .AddXModelBuilder().AddDutchFaker(seed)
        .BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    var nl = p.NL();
    return Enumerable.Range(0, 50).Select(_ => nl.Bsn()).ToList();
}

Assert.Equal(BsnsWithSeed(2026), BsnsWithSeed(2026));      // identical
Assert.NotEqual(BsnsWithSeed(2026), BsnsWithSeed(9999));   // a different seed → a different set
```

> `DutchFaker` registers its **own** seeded `Random`, separate from XFaker's and Bogus's (README
> 21.4), so you can give each faker its own seed and they won't interfere. The provider is the
> isolation boundary: a fresh `ServiceProvider` restarts every counter and reseeds every RNG.

## More Dutch generators

The same `nl.*` surface also covers `Rsin()`, `Vestigingsnummer()`, `AgbCode()`, `BigNummer()`,
`UzoviCode()`, `Bic()`, `Bankrekeningnummer()` (classic 9-digit, bank elfproef), `EanCode()`
(GS1-valid), `Kenteken()`, `VastTelefoonnummer()`, `Paspoortnummer()`, `Rijbewijsnummer()`,
`Provincie()` and `Gemeente()` — see the table in README chapter 21.4.

Want your own checked identifier (an ISBN, a Luhn/IMEI number, a different EAN)? The same building
blocks are public: `Checksums` (mod-11 / Luhn / GS1 / mod-97) and `RandomExtensions`
(`Digits(n)`, `PickFrom(...)`, `FromPattern("??-###-?", letters)`) let you write a one-line generator
in your own `IFaker`, exactly as the Dutch one does (README 21.4).

## Takeaways

- `nl.*` generators emit **valid** Dutch identifiers (elfproef / mod-97 / GS1), available typed
  (`xprovider.NL()`) or as tokens (`"nl.bsn()"`).
- The **same seed reproduces the same set**; the provider is the isolation boundary, and DutchFaker's
  RNG is independent of the other fakers'.
- The public `Checksums` / `RandomExtensions` helpers let you re-validate the data — or roll your own
  checked identifier in one line.

Back to the [scenario index](README.md).
