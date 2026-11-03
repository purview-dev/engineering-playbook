# EF Core Integration

## The rule

When EF Core is the store, it is the boundary. These rules are conditional on a repository that actually
persists data; a stateless CLI or a pure library takes none of them.

| Rule | Statement |
| -- | -- |
| **1** | Use `DbContext` directly. Do not add `IRepository<T>` or a unit-of-work wrapper for testability. |
| **2** | Choose the provider, and the identifier strategy, where the context is registered. Carry the choice as context-options data; never branch on `Database.ProviderName` while the model is being built. |
| **3** | Map a value object once, in the model. Do not repeat a converter or comparer per property. |
| **4** | A generated value has exactly one owner: the application, the store, or a derivation. |
| **5** | Design-time options and runtime options make the same choices, including the provider, the strategy and every `ReplaceService` call. |
| **6** | The model cache key includes everything that varies the model: provider, strategy, tenant and design time. |
| **7** | Migrations are code, reviewed and applied by a deliberate step. The API does not migrate at startup in production. |
| **8** | Integration tests run against a real store. The in-memory provider is not a database. |
| **9** | A connection string is configuration, not source, and never telemetry. |

Rules 2 and 4 are where most of the identifier-specific detail lives. [Well-Known
Identifiers](Well-Known-Identifiers.md) owns the derived case, and [GUID Key
Strategy](Guid-Key-Strategy.md) owns the generated case and the full convention implementation.

## Why

| Rule | Failure it prevents |
| -- | -- |
| 1 | A generic repository hides `Include`, tracking, `SaveChanges` semantics and the query shape, then needs its own tests to prove it forwards correctly. It is not more testable than a real database in a container. |
| 2 | The model is built once and cached. A `Database.ProviderName` branch at model-build time picks one provider's model and serves it to the other. |
| 3 | A converter configured per property is forgotten on the next property, and queries start failing on the one type that was missed. |
| 4 | Two owners overwrite each other: a value generator runs over a derived key, or EF fetches a store value that the application already supplied. |
| 5 | The migration snapshot describes a different model from the one the application runs, so the next migration contains a change nobody intended. |
| 6 | EF serves the first tenant's or strategy's model to the next one. The service-provider cache and the model cache are separate, and both need a key that matches the decision. |
| 7 | An API that migrates at startup mutates a production schema on a deploy, without review, and cannot be rolled back cleanly. |
| 8 | The in-memory provider passes a test that a real store rejects: foreign keys, unique indexes, computed columns, concurrency tokens and SQL semantics are all missing. |
| 9 | A connection string in `appsettings.json` or a log line is a credential disclosure, and rotating it does not remove the copy from the history. |

The design-time failure deserves its own sentence because it is invisible in a green build. A migration is
generated from the design-time model, which is built by `IDesignTimeDbContextFactory` or by the host's
registration. If that path omits a `ReplaceService`, an options extension or the provider, `dotnet ef
migrations add` compares the runtime model with a different model and writes a migration that claims a
schema change that is not real.

## The sample

`samples/Acme.Persistence` is the runnable reference for the whole integration: one `DbContext` per provider
strategy, a model-finalising convention, a value generator that honours value converters, an options
extension, a model cache key, a customizer and a composition method. `just samples` builds and runs it,
including the identifier checks, the foreign-key copy and the explicit `ValueGeneratedNever()` key. It
sits outside `src/` because the reference implementation there is in-memory by design.

The wiring is short enough to read as a rule:

```csharp
services.AddDbContext<AcmeDbContext<TProvider>>((serviceProvider, options) =>
{
	ConfigureStore<TProvider>(options, connectionString);

	options
		.UseSequentialGuids<TProvider>()
		.ReplaceService<IModelCacheKeyFactory, AcmeModelCacheKeyFactory>()
		.ReplaceService<IModelCustomizer, AcmeModelCustomizer<TProvider>>();
});

services.AddSingleton<ISequentialGuidFactory, SequentialGuidFactory<TProvider>>();
```

Four details in that snippet are deliberate:

- **The provider is chosen once.** `ConfigureStore` is the only place that names `UseSqlServer` or
  `UseNpgsql`, and it runs where the context is registered.
- **The strategy is data on the options.** `UseSequentialGuids` adds an extension; the model and the value
  generator read it. No model code asks which provider is in use.
- **The model cache key is replaced.** The service-provider hash separates strategies, but the model cache
  is keyed separately and would otherwise return the first model it built.
- **The application-facing factory is registered separately.** EF's internal registration is not reachable
  from an outbox writer or a correlation-id generator, and a second implementation would be a second
  ordering.

A single-provider application can make the type parameter concrete and skip the extension, but it still
needs the design-time parity and the cache key if the model varies at design time.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| EF Core stays out of the domain, application, contracts and provider layers | The architecture policy's `forbiddenPackagePrefixes`, checked by `PurviewArchitectureTests` |
| The provider is chosen once, at composition | Review, plus a two-provider test that asserts two models |
| A value object is mapped once | The `Purview.ValueObjects` source generator when EF Core is referenced; otherwise review |
| One owner per generated value | Review, plus the tests below in the adopting repository |
| Design-time and runtime options agree | An integration test that builds the model both ways and compares the snapshot |
| The model cache key covers the model's inputs | A test with two strategies and one context type asserts two models |
| Migrations are applied deliberately | Review, and the `Migrator` host in [Project Responsibilities](Project-Responsibilities.md) |
| Integration tests use a real store | The `*.IntegrationTests` project name and the `Purview.Containers` rule in [Testing and Test Data](Testing-And-Test-Data.md) |
| The connection string stays out of source and telemetry | Review; in an adopting repository, a secret scanner and a redaction test turn it into a build failure |

The checks worth writing in an adopting repository are small and specific:

| Check | Shape |
| -- | -- |
| Model covers every key | For each entity type and each GUID key that is not a foreign key, the property is generated on add and has the expected factory. |
| Foreign keys are not generated | A foreign-key GUID keeps the value copied from its principal; it never receives a generated value. |
| Explicit configuration wins | A `ValueGeneratedNever()` key keeps its supplied value through `SaveChanges`. |
| Two strategies, one context type | Different strategies produce different service providers and different models. |
| Design time matches runtime | The design-time model and the runtime model produce the same migration snapshot. |
| Real store semantics | A unique index, a foreign key and a computed column behave as the model says in a container. |

`src/` does not demonstrate any of this. The reference implementation is deliberately in-memory, and the page
says so rather than claiming a check it does not have.

## At your scale

- **One project, low volume.** Use `DbContext` directly, with integer keys or `Guid.NewGuid()`. Create the
  schema with one migration or `EnsureCreated` for a local tool, and do not add a repository, an options
  extension or a strategy. The integration rules that still apply are 1, 4 and 9.
- **One project, a real database, a few queries.** Add value-object converters and a real-provider
  integration test. Keep the provider choice in composition, and keep migrations in the repository even if
  a developer applies them locally.
- **Several layers.** Put the `DbContext`, mappings and migration definitions in `Persistence`; keep the
  provider choice in `Composition` or the host. Add the architecture policy so EF Core cannot leak upward.
- **Two providers or application-generated keys.** Add the options extension, the value generator and the
  model cache key from [GUID Key Strategy](Guid-Key-Strategy.md). That is the point at which the extra
  wiring is earned.
- **Several stores or tenants.** Add a design-time factory per store, a model cache key that includes the
  tenant, and a separate `Migrator` host. Keep the API out of the migration path.

## See also

- [Well-Known Identifiers](Well-Known-Identifiers.md) — the derived owner of a key
- [GUID Key Strategy](Guid-Key-Strategy.md) — the generated owner, and the full convention
- [Contracts and Data Exchange](Contracts-And-Data-Exchange.md) — value objects and their converters
- [Testing and Test Data](Testing-And-Test-Data.md) — real stores and container-backed suites
- [Project Responsibilities](Project-Responsibilities.md) — Persistence owns definitions, Migrator executes them
- [Working with EF Core](https://learn.microsoft.com/en-us/ef/core/) — the framework's own documentation
