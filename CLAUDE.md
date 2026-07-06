# CLAUDE.md

Guidance for working in this repository. Keep it short; deep detail lives in `README.md`.

## What is this

XModelBuilder is a reflection-based .NET framework for building deterministic test
data (Object Mother + Test Data Builder + orchestration). A single generic base class
`ModelBuilder<TBuilder,TModel>` provides a fluent builder for every class; properties/fields
are set via lambdas, string deep-paths (`"Address.Street"`, `"Lines[2].Quantity"`) or
`WithValues` (key/value, e.g. Gherkin tables). Text values are converted culture-aware and
support a mini data language for arrays/sets/dictionaries/object-literals. Integrates with
Bogus and with Reqnroll/SpecFlow. Works with MS DI and standalone via a static provider.

**`README.md` is the canonical, full spec (chapters 1–21).** Consult it for API details,
algorithms and edge cases; update it when behavior changes. (`README-nl.md` is the Dutch
translation of the same spec, kept in sync.)

## Solution layout

| Project | Role |
|---|---|
| `XModelBuilder` | Core library (net10.0) |
| `XModelBuilder.Fakers.XFaker` | Dependency-free deterministic faker (`AddXFaker(seed)`) |
| `XModelBuilder.Fakers.Bogus` | Bogus integration (`AddBogusFaker(seed)`) |
| `XModelBuilder.Reqnroll` / `.SpecFlow` | Gherkin-table integrations (`CreateModel(s)<T>`) |
| `*.UnitTests`, `XModelBuilder.Fakers.UnitTests` | xUnit test projects |
| `XModelBuilder.Benchmarks` | Benchmarks |
| `Demo/XModelBuilder.Demo.Shop` | Demo web shop (ASP.NET Core Web API, EF Core/SQL Server) |
| `Demo/XModelBuilder.Demo.Shop.IntegrationTests` | Reqnroll integration tests of the demo (see `Demo/README.md`) |
| `__old_samples` | Obsolete; do not use |

## Core structure of the main project

- Root: public contracts and core implementation — `IModelBuilder(.cs)`, `ModelBuilder.cs`,
  `IModelBuilderProvider.cs`, `ModelBuilderOptions.cs`, `ModelBuilderAttribute.cs`,
  `IFaker.cs`, `*Extensions.cs`.
- `Default/`: static facades (`For`, `Use`, `Create`), `DefaultModelBuilder`,
  `DefaultModelBuilderProvider` (standalone singleton).
- `DependencyInjection/`: `ServiceCollectionExtensions` (`AddXModelBuilder`, `AddModelBuilder`,
  `UseAsDefaultModelBuilder`, `AddFaker`, …), `ModelBuilderProvider` (**the only place with real
  resolution logic**), `AssemblyScanner`, `ModelBuilderDefaults`, `XModelBuilderIsolation`.
- `Core/` (everything `internal`, except the public `FriendlyNameExtensions`): `Parser`/`DataParser`/
  `CharScanner` (mini data language), `ValueConverter` (conversion + faker tokens), `FakerInvoker`
  (overload resolution + Type/IServiceProvider injection), `StringPathSetter`/`LambdaPathSetter` (deep-paths),
  `Instantiator`, `HelperExtensions`.

## Conventions

- Target platform **net10.0**, `Nullable` enable, `ImplicitUsings` enable — for all projects.
- Tests: **xUnit**. New core features get tests in `XModelBuilder.UnitTests`;
  faker/Gherkin features in the corresponding `*.UnitTests` project.
- **Every** unit test has the comments `// Arrange`, `// Act` and `// Assert` in its body
  (in that order), marking the three phases. This applies to every existing and newly added
  test. Use a block body (not an expression body) so the comments fit; if one phase coincides
  with another (e.g. a one-liner that both invokes and asserts), combine the markers, e.g.
  `// Act & Assert`.
- **Newly added files** get English XML-doc headers on **all `public` and `protected` members**
  (types, methods, properties, constructors, fields): `<summary>`, plus `<param>`/`<typeparam>`
  for each parameter/type-parameter, `<returns>` for a return value, and `<exception>` for each
  exception thrown as part of the contract. `internal`/`private` members may stay brief or
  undocumented. (Fill in existing files when you touch them anyway.) **Exception:** test projects
  (`*.UnitTests`) — there, expressive test names + the AAA comments suffice; XML-doc headers on
  test methods/helpers are not needed.
- `XModelBuilder` has `InternalsVisibleTo` to `XModelBuilder.UnitTests`, so `internal` types
  (the Core layer) are directly testable there.
- The core project deliberately depends on the **full** `Microsoft.Extensions.DependencyInjection`
  (not only `.Abstractions`), because `DefaultModelBuilderProvider` builds a ServiceProvider itself.
- Every builder for a model type has a mandatory, unique `[ModelBuilder("name")]`;
  the default is assigned order-independently with `UseAsDefaultModelBuilder<TBuilder>()`.
- The repository keeps a Dutch README (`README-nl.md`) in sync with the English `README.md`.
  Code, XML-doc comments and error messages are in English.

## Building & testing (PowerShell)

```powershell
dotnet build XModelBuilder.sln
dotnet test  XModelBuilder.sln                      # all test projects
dotnet test  XModelBuilder.UnitTests                # core only
```

## Git

The default branch is `main`. Do not commit or push unless explicitly asked; when a commit is
requested, branch first if appropriate. The demo integration tests need SQL Server and are
intentionally excluded from CI.
