# Scenario 01 — Time-traveling

**Goal.** Generate clock-bound test data (ages/birthdates, "placed on" dates, "is this overdue?")
against a clock *you* control, then move that clock forwards or backwards and watch the very same
entities age — all deterministically.

**Why this works.** XModelBuilder never reads `DateTime.Now`. Every "now"-relative value flows
through an injected `TimeProvider` (README chapter 21.6). The built-in `XFaker` obeys it:
`xfake.AgeBetween(min, max)` computes a birthdate from `clock.GetLocalNow().Date`, *not*
`DateTime.Today` (see `XFakerApi.AgeBetween`). Register your own `TimeProvider` and you own "now"
for the faker **and** for any production service that also takes a `TimeProvider` (the demo's
`OrderService` does — `clock.GetUtcNow()`). Freeze one clock and the whole world stops with it.

> `TimeProvider` is registered with `TryAdd` (always Singleton) by `AddXFaker`, so **a clock you
> register first wins**. It also stays Singleton even under `PerScope` isolation (README 21.1).

## A controllable clock

You don't need an extra package. A `TimeProvider` only has to answer "what is now?"; override
`GetUtcNow()` and pin the local time zone so a machine's local offset can't shift a date across
midnight:

```csharp
public sealed class MovableClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>The current "now". Assign to it to jump; call <see cref="Advance"/> to step.</summary>
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    // Keep GetLocalNow() (and therefore AgeBetween) independent of the host machine's zone.
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}
```

> The standard `Microsoft.Extensions.TimeProvider.Testing` package's `FakeTimeProvider`
> (`SetUtcNow(...)`, `Advance(...)`) is a drop-in alternative — use it if it's already referenced.
> The hand-rolled clock above keeps this scenario dependency-free.

## Build the dataset under a frozen clock

Register the clock **before** `AddXFaker` so its `TryAdd` keeps yours:

```csharp
var clock = new MovableClock(new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero));

var xprovider = new ServiceCollection()
    .AddSingleton<TimeProvider>(clock)   // ours wins over AddXFaker's TryAddSingleton(TimeProvider.System)
    .AddXModelBuilder()
    .AddXFaker(seed: 2026)               // clock-bound ages read `clock`
    .AddBogusFaker(seed: 2026)           // names
    .BuildServiceProvider()
    .GetRequiredService<IModelBuilderProvider>();
```

The domain we'll age:

```csharp
public sealed class Member
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public DateTimeOffset JoinedOn { get; set; }
}
```

Build 5 members whose age at "now" is 18–70. Because `AgeBetween` reads the frozen clock, every
birthdate is anchored to **2026-07-10**:

```csharp
var members = xprovider.BuildMany<Member>(5, (b, i) => b
    .With(m => m.Id,        xprovider.XFake().NewGuid($"member:{i}"))
    .With(m => m.Name,      xprovider.Bogus().Name.FullName())
    .With(m => m.BirthDate, p => DateOnly.FromDateTime(p.XFake().AgeBetween(18, 70)))
    .With(m => m.JoinedOn,  clock.GetUtcNow()));
```

`AgeBetween` returns a `DateTime`; the factory wraps it into a `DateOnly`. Using the string-token
form instead reads just as well when the type is `DateTime`:

```csharp
.With("BirthDate", "xfake.AgeBetween(18,70)")   // same generator, addressed as a token
```

## Work with it further — travel through time

The point of owning the clock is that "current age", "days since joining" and any overdue/expiry
logic are now *functions of the clock*, computed the same way in your assertions as in production.

```csharp
int AgeOf(Member m) =>
    (int)((clock.Now.Date.DayNumber - m.BirthDate.DayNumber) / 365.25);

// Right now (2026-07-10) everyone is within the requested band:
Assert.All(members, m => Assert.InRange(AgeOf(m), 18, 70));

// Jump the whole world forward ten years — no rebuild, the entities are the same objects:
clock.Advance(TimeSpan.FromDays(365 * 10 + 3));   // ~2036-07

Assert.All(members, m => Assert.InRange(AgeOf(m), 28, 80));  // everyone aged exactly 10
```

Because the clock is shared, data you generate *after* moving it lands in the new era, while the
already-built members keep their fixed birthdates — exactly like real life:

```csharp
var newJoiner = xprovider.For<Member>()
    .With(m => m.Name,      "Late Arrival")
    .With(m => m.BirthDate, p => DateOnly.FromDateTime(p.XFake().AgeBetween(18, 70)))
    .With(m => m.JoinedOn,  clock.GetUtcNow())   // now ~2036, not 2026
    .Build();

Assert.True(newJoiner.JoinedOn > members[0].JoinedOn);
```

### Driving production code with the same clock

The demo's `OrderService` takes a `TimeProvider` and stamps `clock.GetUtcNow()` on new orders and
compares against it for overdue logic. In an integration test you register the **same**
`MovableClock` into the app's container, seed data with XModelBuilder, then `clock.Advance(...)` to
push an order past its due date and assert the service now reports it overdue — without waiting a
single real second and with no flaky `DateTime.Now` anywhere.

## Takeaways

- One knob — the registered `TimeProvider` — controls "now" for faker data *and* production services.
- `xfake.AgeBetween` is clock-bound on purpose, so ages stay deterministic and time-travelable.
- Register your clock **before** `AddXFaker` (its `TryAdd` yields to yours); pin `LocalTimeZone` so
  dates don't drift with the host machine.
- Advancing the clock re-dates *new* data while leaving already-built entities fixed.

Next: [02 — companies with addresses (12% differ)](02-companies-with-addresses.md).
