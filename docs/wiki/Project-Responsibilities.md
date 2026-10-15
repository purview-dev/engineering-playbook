# Project Responsibilities

## The rule

A project name states a **responsibility**, optionally qualified by an implementation:

```text
<Responsibility>[.<Implementation>]
```

`Persistence`, `Persistence.SqlServer`, `Providers.GitHub`, `Adapters.Email` are all well-formed. The
responsibility decides the rules; the qualifier is a naming choice and never changes them.

**This is a catalogue of responsibilities, not a list of projects you must create.** A small
application that combines Core, Application and Persistence in one assembly has not broken the rule —
it has one project carrying three responsibilities, which is usually correct. The catalogue tells you
what each responsibility *is* so that you can tell when splitting one out would buy something.

The authoritative definition is [`.config/architecture-policy.json`](https://github.com/purview-dev/engineering-playbook/blob/main/.config/architecture-policy.json).
The tables on this page are checked against it — see [How it is enforced](#how-it-is-enforced).

## Why

Two failures, equally common and opposite.

**The catch-all project.** `Infrastructure`, `Services`, `Helpers`, `Common`. These name a *category*,
so nothing can be excluded from them. Every technical class is plausibly infrastructure, every
coordinating class is plausibly a service, and both grow until no one can say what the project is for.
A responsibility name can be violated; a category name cannot, which is exactly why it is useless.

**Architecture bought in advance.** Four projects, a repository interface and a mapping layer around
one table and one caller. Nothing in it is wrong in isolation; all of it is cost paid at every future
change against a failure that never arrives.

A catalogue addresses both: it gives each project a name that can be wrong, and it makes "do I need
this project?" a question with an answer rather than a matter of taste.

## The catalogue

`A → B` means **A depends on B at compile time**. It is not a runtime call, a DI registration, an
HTTP request or an Aspire orchestration edge.

| Responsibility | Category | Also known as | May depend on |
| -- | -- | -- | -- |
| `Core` | domain | `Domain` | Shared |
| `Abstractions` | domain | — | Core, Shared |
| `Application` | application | `UseCases` | Core, Abstractions, Contracts, Shared |
| `Contracts` | application | — | Core, Shared |
| `Persistence` | infrastructure | — | Core, Abstractions, Application, Shared |
| `Providers` | infrastructure | `Integrations` | Core, Abstractions, Application, Shared |
| `Adapters` | infrastructure | — | Core, Abstractions, Application, Shared |
| `Composition` | composition | — | Core, Abstractions, Application, Contracts, Persistence, Providers, Adapters, Shared |
| `API` | host | — | Core, Abstractions, Application, Contracts, Composition, ServiceDefaults, Shared, Persistence, Providers, Adapters |
| `Worker` | host | — | Core, Abstractions, Application, Contracts, Composition, ServiceDefaults, Shared, Persistence, Providers, Adapters |
| `Migrator` | host | — | Core, Abstractions, Persistence, Composition, ServiceDefaults, Shared |
| `AppHost` | host | — | API, Worker, Migrator, WebApp, Shared |
| `ServiceDefaults` | host | — | Shared |
| `WebApp` | host | — | Contracts, ServiceDefaults, Shared |
| `Shared` | shared | — | *(nothing)* |

### When each one earns its own project

| Responsibility | Split it out when | Leave it combined when |
| -- | -- | -- |
| `Core` | A domain model exists at all | The app is a thin utility with no domain rules |
| `Abstractions` | A contract has two implementations, or a consumer must not see the implementation | One implementation, no cross-boundary consumer — keep the interface beside it |
| `Application` | Workflows are reused by more than one host | It would only forward CRUD without adding behaviour |
| `Contracts` | A client must compile against the contract without taking the server | The only consumer is the API host |
| `Persistence` | Persistence exists — it is what Core must not reference | Single-project utility writing to a file |
| `Providers` | Two platforms with different SDKs or lifecycles | One platform, or small clients sharing a lifecycle |
| `Adapters` | A capability has more than one technology behind it | One capability, one technology |
| `Composition` | More than one host composes the same implementations | One host — compose in the host |
| `Migrator` | Schema changes are applied by a deliberate deployment step | Development-only, migrate on startup |
| `ServiceDefaults` | More than one host shares operational defaults | One host |
| `Shared` | A domain-independent primitive is used by two or more responsibilities | Almost always — it is the easiest project to create and the hardest to keep coherent |

### Diagram A — the responsibility catalogue

Grouped by category. **No project here is mandatory.**

```text
flowchart TB
    subgraph domain["Domain"]
        Core["Core<br/>(aka Domain)"]
        Abstractions["Abstractions"]
    end
    subgraph application["Application"]
        Application["Application<br/>(aka UseCases)"]
        Contracts["Contracts"]
    end
    subgraph infrastructure["Infrastructure - a category, not a project"]
        Persistence["Persistence"]
        Providers["Providers<br/>(aka Integrations)"]
        Adapters["Adapters"]
    end
    subgraph composition["Composition"]
        Composition["Composition"]
    end
    subgraph hosts["Hosts"]
        API["API"]
        Worker["Worker"]
        Migrator["Migrator"]
        AppHost["AppHost"]
        ServiceDefaults["ServiceDefaults"]
        WebApp["WebApp"]
    end
    subgraph shared["Shared"]
        Shared["Shared"]
    end
```

### Diagram B — compile-time dependency direction

Every arrow means *depends on*. Note that nothing points into Core except by permission, and nothing
points at Composition at all.

```text
flowchart RL
    Abstractions --> Core
    Application --> Core
    Application --> Abstractions
    Application --> Contracts
    Contracts --> Core

    Persistence --> Core
    Persistence --> Abstractions
    Persistence --> Application
    Providers --> Abstractions
    Adapters --> Abstractions

    Composition --> Application
    Composition --> Persistence
    Composition --> Providers
    Composition --> Adapters

    API --> Composition
    API --> Contracts
    Worker --> Composition
    Migrator --> Persistence
    WebApp --> Contracts

    AppHost -.->|"orchestrates, not a reference"| API
    AppHost -.->|"orchestrates, not a reference"| Worker
```

## Naming

### Diagram H — generic or qualified?

```text
flowchart TD
    A["New implementation of a responsibility"] --> B{"More than one<br/>implementation?"}
    B -- no --> C["Use the generic name<br/>Persistence, Providers, Adapters"]
    B -- yes --> D{"Do they differ in<br/>dependencies, lifecycle<br/>or deployment?"}
    D -- no --> E["One project, several implementations inside<br/>still the generic name"]
    D -- yes --> F["Qualify each one<br/>Persistence.SqlServer, Persistence.PostgreSql"]
    F --> G{"Is there genuinely<br/>shared code?"}
    G -- yes --> H["Add the unqualified project too<br/>Persistence + Persistence.SqlServer"]
    G -- no --> I["Qualified projects only<br/>no empty parent"]
```

Three rules fall out of that tree, and all three are departures from what people usually assume:

- **Do not qualify a single implementation.** `Persistence` is correct even though it happens to use
  SQL Server. Rename it when a second provider arrives, not in anticipation.
- **Do not create an empty parent.** `Persistence.SqlServer` and `Persistence.PostgreSql` with no
  shared code need no `Persistence` project.
- **Multiple implementations do not require multiple assemblies.** Two small adapters with the same
  dependencies can live in one project.

### Diagram C — single implementation

```text
flowchart RL
    Persistence["Persistence<br/>(SQL Server inside - not in the name)"] --> Core
    Providers["Providers<br/>(one platform)"] --> Abstractions
    Adapters["Adapters"] --> Abstractions
    Abstractions --> Core
    Composition --> Persistence
    Composition --> Providers
    Composition --> Adapters
    API --> Composition
```

### Diagram D — multiple implementations

The unqualified `Persistence` appears only because there is shared mapping code. `Providers` has no
unqualified project, because `GitHub` and `AzureDevOps` share nothing.

```text
flowchart RL
    PersistenceShared["Persistence<br/>shared mappings"] --> Core
    PersistenceSql["Persistence.SqlServer"] --> PersistenceShared
    PersistencePg["Persistence.PostgreSql"] --> PersistenceShared

    ProvidersGitHub["Providers.GitHub"] --> Abstractions
    ProvidersAzdo["Providers.AzureDevOps"] --> Abstractions
    Abstractions --> Core

    Composition --> PersistenceSql
    Composition --> PersistencePg
    Composition --> ProvidersGitHub
    Composition --> ProvidersAzdo
    API --> Composition

    ProvidersGitHub -. "forbidden: siblings stay independent" .-x ProvidersAzdo
```

### Alternative names

`Domain` for Core, `UseCases` for Application and `Integrations` for Providers are recognised and
need no change. **Do not rename an established project merely to match canonical terminology** — the
rename costs more than the consistency is worth.

These names are discouraged as project names, because each names a category rather than a
responsibility: `Infrastructure`, `Services`, `Managers`, `Models`, `Helpers`, `Common`, `Utils`,
`Utilities`.

`Infrastructure` is the one worth calling out. It is a useful *category* — it covers Persistence,
Providers and Adapters — and a poor *project*, because everything technical qualifies.

## Providers versus Adapters

The distinction is the one people get wrong most often, and it is not about the vendor.

- A **Provider** integrates a *named external platform*: GitHub, Azure DevOps. The platform is part of
  the requirement — "sync issues from GitHub".
- An **Adapter** implements an *application-defined capability* using some technology: send email,
  store a blob. The capability is the requirement; the technology is interchangeable.

The same vendor can be either. Azure Blob Storage behind `IFileStore` is an Adapter. The Azure DevOps
work-item API, where the product explicitly integrates Azure DevOps, is a Provider.

### Diagram E — Providers versus Adapters

```text
flowchart TD
    subgraph req["What the requirement says"]
        R1["'Sync issues from GitHub'<br/>the platform is named"]
        R2["'Send the user an email'<br/>the capability is named"]
    end

    R1 --> P["Provider<br/>Providers.GitHub"]
    R2 --> A["Adapter<br/>Adapters.Email implements IEmailSender"]

    P --> PA["Owns: client, auth, rate limits,<br/>provider models, error translation"]
    A --> AA["Owns: one technology behind<br/>an application-defined interface"]

    PA --> N1["Swapping it changes the product"]
    AA --> N2["Swapping it changes nothing the user sees"]
```

## Composition and runtime

Composition is the only project allowed to know every implementation, and nothing is allowed to
depend on it. That is what keeps implementation choice in one place.

### Diagram F — composition and runtime

Solid arrows are compile-time references. Dashed arrows are runtime resolution, which runs in the
opposite direction and is the whole point of the arrangement.

```text
flowchart TD
    Abstractions["Abstractions<br/>IConventionRepository"]
    Application["Application"]
    PersistenceSql["Persistence.SqlServer"]
    Composition["Composition"]
    API["API"]

    Application -->|depends on| Abstractions
    PersistenceSql -->|implements| Abstractions
    Composition -->|references| PersistenceSql
    Composition -->|references| Application
    API -->|references| Composition

    Composition -.->|"registers at runtime"| Resolve["IConventionRepository resolves<br/>to the SQL Server implementation"]
    Application -.->|"receives at runtime"| Resolve
```

Application never references `Persistence.SqlServer`. It receives an implementation it cannot name.

## Persistence and migration

Persistence **owns** migration definitions. Migrator **executes** them. Splitting those two is what
stops an API startup path from mutating a production schema.

### Diagram G — persistence and migration

```text
flowchart TD
    Persistence["Persistence<br/>DbContext, mappings, migration definitions"]
    Migrator["Migrator<br/>executes migrations, controlled seeding"]
    API["API"]
    DB[("Database")]

    Persistence -->|"migrations live here"| Migrator
    Migrator -->|"applies schema, deliberate step"| DB
    API -->|"reads and writes only"| DB

    API -.->|"must NOT apply migrations at startup in production"| DB
```

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| Every production project maps to a responsibility or a recorded waiver | `ProjectDependencyPolicyTests` |
| Declared project references respect the dependency matrix | `ProjectDependencyPolicyTests` |
| Sibling implementations stay independent | `ProjectDependencyPolicyTests` |
| Nothing depends on Composition except hosts | `ProjectDependencyPolicyTests` |
| Forbidden framework packages stay out of a responsibility | `ProjectDependencyPolicyTests` |
| The project graph has no cycles | `ProjectDependencyPolicyTests` |
| Catch-all project names are rejected | `ProjectDependencyPolicyTests` |
| Production code does not reference a test project | `ProjectDependencyPolicyTests` |
| Every waiver names a real project, a rule, and a substantive reason | `ProjectDependencyPolicyTests` |
| This page's tables match the policy file | `PolicyDocumentationTests` |
| A responsibility is split only when it is justified | **Review** |

The tests read `.config/architecture-policy.json`. Nothing on this page is a second source of truth —
a consistency test fails if the tables drift from the policy.

### Waivers

A repository that predates the policy will violate it, and suppressing that silently would make the
policy a lie. A waiver records the departure instead:

```json
{
  "project": "Infrastructure",
  "mapsTo": "Adapters",
  "rules": ["naming", "discouragedName"],
  "reason": "...",
  "reviewedOn": "2026-10-15"
}
```

A waiver excuses the **name only**. `mapsTo` keeps the project classified, so every dependency rule
still applies to it. This repository's own `Infrastructure` project is the worked example: it holds
one adapter, so it is not the catch-all the rule is aimed at, and renaming it would churn the wiki,
`AGENTS.md` and `NamespaceRemoveSuffix` for no gain.

### Diagram I — architecture enforcement

```text
flowchart TD
    A["Discover *.csproj under src/"] --> B["Read ProjectReference and PackageReference"]
    B --> C["Classify by name<br/>responsibility, alias, or waiver mapsTo"]
    C --> D["Load .config/architecture-policy.json"]
    D --> E["Evaluate rules<br/>matrix, siblings, composition, packages, cycles, names"]
    E --> F{"Violations?"}
    F -- yes --> G["Fail with project, rule, offending reference<br/>and the direction it should run"]
    F -- no --> H["Pass"]
    D --> I["PolicyDocumentationTests<br/>wiki tables match the policy"]
```

## At your scale

- **A small application.** One or two projects. Use the catalogue to *name* them, not to multiply
  them. `Core` plus a host is a complete and correct architecture.
- **A medium application.** Split out the responsibility whose boundary you keep tripping over —
  usually Persistence, so Core and Application stop seeing EF Core.
- **A large application.** The full catalogue earns its place, and the tests stop the matrix drifting
  as teams change.
- **Any size.** Copy the waiver mechanism before you copy the rules. A policy with no honest escape
  hatch gets suppressed wholesale the first time it is inconvenient.

## See also

- [Architecture as Tests](Architecture-As-Tests.md) — how a dependency rule becomes a failing build
- [Repository Shape](Repository-Shape.md)
- [Engineering Principles](Engineering-Principles.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
