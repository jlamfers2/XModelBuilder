# Scenario 03 — A related graph: customers with orders

**Goal.** Build a small graph — 10 customers, each with a handful of orders — where the **foreign
keys line up** (`order.CustomerId == customer.Id`), the **ids are stable** across runs, and the
**invoice numbers** form a readable running sequence.

**The two kinds of "deterministic" this uses (README chapter 21.2):**

- **Name-based GUIDs** — `xfake.NewGuid("customer:3")`. The *same key* always yields the *same*
  GUID, regardless of call order or parallelism. Perfect for keys and foreign keys: you can mint the
  parent's id and the child's `CustomerId` from the same key and they match by construction.
- **Readable sequences** — `xfake.Sequence("INV-{0:0000}")` yields `INV-0001`, `INV-0002`, … from a
  counter kept per format string. Great for human-facing running numbers that must be unique across
  the whole dataset.

## The domain

```csharp
public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Order> Orders { get; set; } = [];
}

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }   // foreign key back to Customer
    public string Number { get; set; } = "";
    public DateTime PlacedOn { get; set; }
    public decimal Total { get; set; }
}
```

## Build the graph

Uses the shared provider from the [scenario index](README.md#the-one-setup-they-share)
(`AddXFaker` + `AddBogusFaker`, seed 2026). Each customer's id comes from a stable key; each of its orders is built with `BuildMany` (form (b),
a fresh builder per order) and stamped with that same customer's id as the foreign key.

```csharp
var xfake = xprovider.XFake();

var customers = Enumerable.Range(0, 10).Select(i =>
{
    var customer = xprovider.For<Customer>()
        .With(c => c.Id,   xfake.NewGuid($"customer:{i}"))       // stable id from a key
        .With(c => c.Name, xprovider.Bogus().Company.CompanyName())
        .Build();

    var orderCount = xfake.IntBetween(1, 5);                     // seeded 1..5 orders

    customer.Orders = xprovider.BuildMany<Order>(orderCount, (b, j) => b
        .With(o => o.Id,         xfake.NewGuid($"order:{i}:{j}"))          // stable per (customer, index)
        .With(o => o.CustomerId, customer.Id)                              // FK matches the parent
        .With(o => o.Number,     xfake.Sequence("INV-{0:0000}"))          // INV-0001, INV-0002, …
        .With(o => o.PlacedOn,   p => p.XFake().DateBetween(
                                        new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)))
        .With(o => o.Total,      p => p.Bogus().Finance.Amount(10m, 500m)))
        .ToList();

    return customer;
}).ToList();
```

A few things worth noting:

- **`NewGuid(key)` vs. `NewGuid()`.** The keyed form is used here because we *want* the same entity
  to keep the same id on every run and to reference it from the child. (The un-keyed `NewGuid()` is
  seed-and-order dependent — fine for "just an id", wrong for a stable key.)
- **`Sequence` is global to the faker.** The counter for `"INV-{0:0000}"` keeps climbing across all
  customers, so invoice numbers are unique across the *whole* dataset (INV-0001…INV-00NN), not
  restarted per customer — usually exactly what you want for running numbers.
- **`BuildMany(count, (b, j) => …)`** re-evaluates the factories per order, so `Sequence`, `NewGuid`
  and `DateBetween` each advance/resolve once per child.

> Prefer tokens for the leaf values? The same generators work as strings in a Gherkin table or
> `With(string, string)`: `"nl.bsn()"`, `"bogus.finance.amount(10,500)"`, `"xfake.sequence(INV-{0:0000})"`.
> Reserved characters in a key (`:`) must be quoted or replaced (README 21.6) — e.g.
> `"xfake.newguid(order-3-0)"` or `"xfake.newguid(\"order:3:0\")"`.

## Work with it further

The graph is internally consistent by construction, so joins and roll-ups just work:

```csharp
// Every order points back to an existing customer:
var customerIds = customers.Select(c => c.Id).ToHashSet();
Assert.All(customers.SelectMany(c => c.Orders),
           o => Assert.Contains(o.CustomerId, customerIds));

// Invoice numbers are unique and gap-free across the whole set:
var numbers = customers.SelectMany(c => c.Orders).Select(o => o.Number).ToList();
Assert.Equal(numbers.Count, numbers.Distinct().Count());

// Revenue per customer:
var revenue = customers.ToDictionary(c => c.Name, c => c.Orders.Sum(o => o.Total));

// Re-derive a known customer's id anywhere, WITHOUT holding a reference — same key, same GUID:
var acmeOrders = customers.SelectMany(c => c.Orders)
    .Where(o => o.CustomerId == xfake.NewGuid("customer:3"))
    .ToList();
```

That last trick is the real payoff of name-based ids: a *second* piece of code (a seeding script, a
Gherkin `Given`, an assertion) can address "customer 3" by the same key and land on the same GUID,
so datasets built in different places still join up.

### Persisting the graph

Because `CustomerId` is already set, the graph maps cleanly onto a relational store: hand the list
to EF Core (`db.Customers.AddRange(customers); db.SaveChanges();`) and the parent/child rows and
foreign keys are already coherent — no post-insert fix-up of ids. Re-seeding with the same seed
reproduces the identical rows, which keeps integration tests stable.

## Takeaways

- Use **name-based `NewGuid(key)`** for anything that is a key or is referenced — ids stay stable
  and foreign keys match by construction.
- Use **`Sequence(format)`** for human-readable running numbers that must be globally unique.
- Build children with **`BuildMany`** and stamp the parent's id as the FK; the graph is consistent
  before it ever hits a database.

Next: [04 — a deterministic Dutch dataset](04-deterministic-dutch-dataset.md).
