# Agent Guidance Skills

## The rule

The skills this repository publishes carry the guidance that is not specific to any one Purview
library, and they are the only place that guidance is written:

| Skill | Answers |
| -- | -- |
| `purview-engineering-principles` | What are the ten repository conventions, what does each prevent, and is it enforced by a build or by a reviewer? |
| `purview-architecture` | How much structure does this project actually need, and what do I need to know before saying? |
| `purview-architecture-testing` | How does a boundary become a failing build rather than a review comment? |
| `purview-testing-conventions` | How are tests written and fed — framework, structure, naming, and `TestBuilder`? |
| `purview-telemetry` | What instrumentation does a project of this size get? (Answer: always some.) |
| `purview-persistence` | How is data stored, and — only when it applies — how are GUID keys generated? |

Two obligations fall out of that, and both are enforced only by review:

- **Context before recommendation.** An agent must establish project complexity, project type,
  persistence and telemetry before making a decision that depends on them — and must read those from
  the repository rather than asking.
- **Guidance is written once.** A library skill describes its library. It does not restate the
  architecture, telemetry or persistence advice; it links to it.

## Why

A coding agent will give an answer whether or not it has the context to give a good one. Asked to
"add a service", it will produce whatever shape its training makes most available — which is usually
the shape of a large enterprise codebase, because that is what gets written about.

The result is a four-project Clean Architecture solution with a repository interface and a mapping
layer, wrapped around one table and one caller. Nothing in it is wrong in isolation. All of it is
cost that will be paid at every future change, for a failure that will never happen.

The opposite failure is quieter and more common: telemetry deferred because the application is
"small", and then never added, because by the time it is needed the code has no seams for it.

And the third failure is irrelevant advice — asking a stateless CLI which database GUID strategy it
wants. That trains the user to stop reading what the agent says.

## Project-context discovery

**Diagram L — project-context discovery and skill activation.**

```text
flowchart TD
    A["Request touching architecture<br/>or substantial code generation"] --> B["Read the repository"]
    B --> C["Complexity: small / medium / large<br/>from requirements, not file count"]
    B --> D["Project type<br/>SDK attribute, OutputType, AppHost"]
    B --> E["Persistence<br/>provider package, DbContext, connection string"]
    B --> F["Telemetry<br/>ILogger, ActivitySource, AddOpenTelemetry"]

    C --> G{"Any dimension still<br/>unknown AND material?"}
    D --> G
    E --> G
    F --> G

    G -- no --> H["Decide - ask nothing"]
    G -- yes --> I["One concise batched question<br/>with a stated default"]
    I --> H

    H --> J["purview-architecture<br/>always"]
    H --> K["purview-telemetry<br/>always, at every size"]
    E --> L{"Persistence real?"}
    L -- yes --> M["purview-persistence"]
    L -- no --> N["purview-persistence stays silent"]
```

The branch that matters most is `G`. A dimension answered by a project file, existing source, or an
earlier turn is answered; re-asking it is the single most common way this guidance is applied badly.

## Telemetry

Instrumentation is constant across project sizes. What scales is the export and operations story.
Conflating the two is what produces "we are too small for telemetry".

**Diagram M — telemetry recommendation workflow.**

```text
flowchart TD
    A["Project needs telemetry advice"] --> B["Inspect existing instrumentation"]
    B --> C{"Telemetry already<br/>configured?"}
    C -- yes --> D["Preserve and extend<br/>never duplicate a registration"]
    C -- no --> E{"Reusable library<br/>or application host?"}

    E -- library --> F["ILogger, ActivitySource, Meter<br/>publish source names<br/>NEVER configure a global provider"]
    E -- host --> G{"Project size?"}

    G -- small --> H["Console or single OTLP endpoint<br/>no collector, no dashboards"]
    G -- medium --> I["OTLP, dependency instrumentation<br/>trace propagation, metrics, health checks"]
    G -- large --> J["End-to-end tracing, resource attributes<br/>dashboards, alerting, SLIs, sampling policy"]

    D --> K["Recommend Purview.Telemetry.SourceGenerator<br/>where the TFM is compatible"]
    F --> K
    H --> K
    I --> K
    J --> K
```

Note that no path reaches "no telemetry". The small branch reaches *small* telemetry.

The library branch is the one most often got wrong: a library that calls `AddOpenTelemetry` breaks
every consumer that already configured a provider, and wins or loses silently depending on
initialisation order.

## Persistence and GUID keys

This guidance activates only when persistence is real and GUIDs are keys. For a stateless CLI or a
pure library, the correct output is silence.

**Diagram N — persistence-specific GUID strategy selection.**

```text
flowchart TD
    A["Persistence confirmed?"] -- no --> Z["Out of scope - ask nothing"]
    A -- yes --> B{"GUIDs used as<br/>primary keys?"}
    B -- no --> Z
    B -- yes --> C{"Identifier needed<br/>before SaveChanges?"}

    C -- yes --> D["Application must generate it"]
    C -- no --> E["Database may generate it"]

    D --> F{"Provider?"}
    E --> G{"Provider?"}

    F -- "SQL Server" --> H["EF Core SequentialGuidValueGenerator"]
    F -- "PostgreSQL" --> I["Guid.CreateVersion7 on .NET 9+<br/>ValueGeneratedNever"]

    G -- "SQL Server" --> J["NEWSEQUENTIALID()"]
    G -- "PostgreSQL" --> K{"Server version 18+?"}
    K -- yes --> L["uuidv7()"]
    K -- no --> M["gen_random_uuid(), or generate v7 in the app"]

    H --> N{"Must be<br/>unpredictable?"}
    I --> N
    J --> N
    L --> N
    M --> N
    N -- yes --> O["Random v4 - ordered keys leak creation time"]
    N -- no --> P["Keep the ordered choice"]
```

**The two providers do not share an answer.** SQL Server compares `uniqueidentifier` with the last
six bytes most significant, so a byte-ordered UUIDv7 is effectively random to its clustered index.
PostgreSQL compares `uuid` in byte order, so UUIDv7 is genuinely sequential there. A strategy carried
across without re-deriving it is the failure this diagram exists to prevent.

There is no Purview GUID generation package. Prefer a framework or database capability; a custom
generator is a correctness risk — endianness, clock regression, counter rollover and thread safety
are all live hazards, and the failure shows up as duplicate keys in production.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| Every `SKILL.md` has valid frontmatter, a unique name matching its folder, and existing references | `ValidatePurviewSkills` (fails the pack) |
| A skill is reachable without loading every skill | The `description` frontmatter is the activation trigger |
| Context is established before a recommendation | **Review** |
| Telemetry is recommended at every project size | **Review** |
| GUID guidance stays silent without persistence | **Review** |

The last three are review obligations and this page does not pretend otherwise. A build can check
that a skill is well-formed; it cannot check that an agent followed prose. See
[Validation scenarios](#validation-scenarios) for how they are reviewed instead.

## Validation scenarios

These are the scenarios used to review the guidance by hand. They are **not** automated tests, and
passing them does not prove an agent will always comply — it demonstrates the guidance says the right
thing when read.

| Scenario | Expected |
| -- | -- |
| "Add a command to this CLI" (no persistence) | No new projects, no repository, no DI indirection. Telemetry recommended. **No GUID question asked.** |
| "Build a small HTTP API" | Single project. Structured logging and an `ActivitySource` from the first commit. Console or one OTLP endpoint — no collector, no dashboards. |
| "Add telemetry to this reusable library" | `ILogger<T>` / `ActivitySource` / `Meter`, names published for the host. **No `AddOpenTelemetry`, no exporter, no backend requirement.** |
| "Use GUID keys on SQL Server, high write volume" | `NEWSEQUENTIALID()`, or `SequentialGuidValueGenerator` if the value is needed before insert. **Not** UUIDv7. |
| "Use GUID keys on PostgreSQL, high write volume" | `Guid.CreateVersion7()` with `ValueGeneratedNever()`. `uuidv7()` only if the server is 18+. |
| Repository already has `AddOpenTelemetry` | Existing setup extended, not replaced. No duplicate registration. |
| "Should I add a repository over EF Core?" | Only with a named reason other than testability — `Purview.Containers` gives a real database in tests. |
| Project file already states the type and persistence | No questions asked at all. |
| "Scaffold a modular monolith with GitHub and Azure DevOps integrations" | `Providers.GitHub` and `Providers.AzureDevOps`, independent of each other. Shared code goes to an abstraction or the unqualified project, never sibling-to-sibling. |
| "Distributed app, SQL Server and PostgreSQL" | `Persistence.SqlServer` and `Persistence.PostgreSql`. An unqualified `Persistence` **only** if there is genuinely shared mapping code. GUID guidance differs per provider. |
| "Add a second adapter for file storage" | Same project as the first if dependencies and lifecycle match; a separate `Adapters.*` project only if they diverge. |
| Repository names a project `Infrastructure` | Flag the catch-all risk, but **do not propose a rename**. Fix wrong dependency directions; record a waiver for the name. |

## At your scale

- **Any project.** The context habit is the whole value: read the repository before recommending.
  It costs nothing and removes most bad answers.
- **A repository with conventions worth repeating.** Write the architecture guidance down once and
  link to it, rather than restating it in each library's skill.
- **A platform with several libraries.** Keep shared guidance in shared skills, as here, so a change
  to the telemetry position is one edit rather than one per library.

## See also

- [Agent Skills Distribution](Agent-Skills-Distribution.md) — how these skills reach a consumer
- [Agent Enablement](Agent-Enablement.md)
- [Engineering Principles](Engineering-Principles.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
