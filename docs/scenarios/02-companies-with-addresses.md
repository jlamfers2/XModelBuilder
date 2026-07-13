# Scenario 02 — 100 companies with addresses, 12% with a separate invoice address

**Goal.** Build 100 companies. Each has a **visiting address**. For most of them the **invoice
address** is the same location; for roughly **12%** it is a *different* address. The split must be
deterministic — the same seed always produces the same companies and the same ~12%.

**The idea.** Two ingredients:

1. `xfake.Bool(truePercent)` — a *seeded* boolean that is true in about `truePercent`% of draws
   (`XFakerApi.Bool`). Draw it once per company to decide "does the invoice address differ?".
2. A **`Build()` override** (README chapter 13) — the idiomatic home for cross-field logic. It runs
   after all `With` values are applied, so the visiting address already exists and we can either
   reuse it or generate a fresh invoice address.

Doing it in a custom builder means the whole 100-company set is just `BuildMany<Company>(100)` — the
12% rule lives in one place and every consumer gets it for free.

## The domain

Role (visiting vs. invoice) is expressed by *which property* holds the address, so `Address` itself
stays role-agnostic and reusable:

```csharp
public sealed class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Address VisitingAddress { get; set; } = null!;
    public Address InvoiceAddress  { get; set; } = null!;
}

public sealed class Address
{
    public string Street      { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostalCode  { get; set; } = "";
    public string City        { get; set; } = "";
    public string Country     { get; set; } = "NL";
}
```

## The builder that encodes the 12% rule

The provider arrives as a primary-constructor parameter (`xprovider`) and is passed to the base; the
subclass captures it, so `Build()` can reach the seeded fakers through it.

```csharp
using Microsoft.Extensions.Options;
using XModelBuilder;
using XModelBuilder.Fakers.XFaker;
using XModelBuilder.Fakers.Bogus;
using XModelBuilder.Fakers.Dutch;

[ModelBuilder("company")]
public sealed class CompanyBuilder(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xprovider)
    : ModelBuilder<CompanyBuilder, Company>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(c => c.Id,   xprovider.XFake().NewGuid());          // seeded, stable per build order
        With(c => c.Name, p => p.Bogus().Company.CompanyName());
        With(c => c.VisitingAddress, p => NewAddress(p));        // every company sits somewhere
    }

    public override Company Build()
    {
        var company = base.Build();                              // Id, Name, VisitingAddress set

        // ~12% of companies bill to a different address than where they sit.
        var invoiceDiffers = xprovider.XFake().Bool(truePercent: 12);

        company.InvoiceAddress = invoiceDiffers
            ? NewAddress(xprovider)          // an independent invoice address
            : company.VisitingAddress;       // same location bills itself

        return company;
    }

    private static Address NewAddress(IModelBuilderProvider p) =>
        p.For<Address>()
            .With(a => a.Street,      x => x.Bogus().Address.StreetName())
            .With(a => a.HouseNumber, x => x.Bogus().Address.BuildingNumber())
            .With(a => a.PostalCode,  x => x.NL().Postcode())     // valid Dutch postcode "1234 AB"
            .With(a => a.City,        x => x.Bogus().Address.City())
            .Build();
}
```

Register it and build the set:

```csharp
var xprovider = new ServiceCollection()
    .AddXModelBuilder()
    .AddModelBuilder<CompanyBuilder>()   // used automatically for Company
    .AddXFaker(seed: 2026)
    .AddBogusFaker(seed: 2026)
    .AddDutchFaker(seed: 2026)
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();

var companies = xprovider.BuildMany<Company>(100);   // form (b): a fresh builder per company
```

> If the invoice address must be a *separate object with identical values* (e.g. two rows in EF),
> replace `company.VisitingAddress` in the non-differing branch with a copy of it. Making `Address`
> a `record` lets you write `company.VisitingAddress with { }` for a value-equal clone. Sharing the
> instance (as above) is simplest when the dataset only lives in memory.

## Why a `Build()` override rather than per-index `configure`?

`BuildMany(100, (b, i) => …)` can vary configuration by index, but the invoice address depends on a
*value produced during this same build* (the visiting address). `Build()` runs after those values
are applied, so it's the natural place for "field B depends on field A". Keeping it in the builder
also means the rule is not re-implemented at every call site. (For a member without a setter you'd
assign via the protected `SetMember(model, x => x.Member, value)` helper instead of a plain
assignment — see README chapter 13.)

## Work with it further

Everything below is exact and reproducible for `seed: 2026` — rerun and you get the identical set.

```csharp
// How many bill elsewhere? A binomial draw around 12 out of 100; fixed by the seed.
var differing = companies.Count(c => !ReferenceEquals(c.InvoiceAddress, c.VisitingAddress));
// -> a stable number near 12 (the same value on every run with this seed)

// Group the outliers by their invoice city, e.g. to eyeball the spread:
var byInvoiceCity = companies
    .Where(c => !ReferenceEquals(c.InvoiceAddress, c.VisitingAddress))
    .GroupBy(c => c.InvoiceAddress.City)
    .ToDictionary(g => g.Key, g => g.Count());

// Pull a company that DOES differ to drive a "ship here, bill there" test path:
var splitBilling = companies.First(c => !ReferenceEquals(c.InvoiceAddress, c.VisitingAddress));
```

Because the split is deterministic you can assert on it directly (`differing` is a known constant
for the seed), snapshot the whole list to a golden file, or feed it into a repository seeder knowing
the exact shape of the data your tests will see.

### Tuning the fraction

The 12% is one number in one place. Change `Bool(truePercent: 12)` to `Bool(25)` for a quarter, or
lift it to a constructor/option on `CompanyBuilder` if different suites need different mixes. The
determinism guarantee is unchanged — only the expected count moves.

## Takeaways

- `xfake.Bool(percent)` turns "a fraction of the set behaves differently" into one seeded draw.
- A `Build()` override is where cross-field rules (invoice-vs-visiting) belong; `BuildMany` then
  scales it to any N for free.
- Address role lives in the *owning property*, keeping `Address` reusable for both slots.

Next: [03 — a related graph (customers → orders)](03-related-graph-customers-orders.md).
