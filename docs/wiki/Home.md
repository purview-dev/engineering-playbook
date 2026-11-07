# Engineering Playbook

This is the playbook for building and maintaining a Purview repository. It records the conventions the
projects under `purview-dev` are held to: how a repository is laid out, how its architecture is enforced
by tests rather than by review, how a change is verified in tiers, how data crosses a boundary, how
tests are written and fed, and how a repository is made legible to a coding agent.

The conventions were previously discoverable only by reading the repositories and inferring the pattern
from the repetition. They are stated here once, with the evidence and the trade-offs attached, and
scaled so that a repository adopting them can take the part that fits.

## How this wiki is organised

The wiki is one folder per topic, and the sidebar and the tables below carry the same sections in the
same order. `_Sidebar.md` is what the published site reads for page order, so a page that is not listed
there has no place in the navigation. A page's folder is part of its published URL
(`foundations/Repository-Shape.md` is published under `foundations/`), so a page moves folder only when
it changes topic.

| Section | Where it lives |
| -- | -- |
| Get started | the wiki root |
| Repository foundations | `foundations/` |
| Data and persistence | `data/` |
| Testing | `testing/` |
| Working with agents | `agents/` |
| Adoption and governance | `governance/` |
| The reference implementation | `examples/` |

Four pages stay at the wiki root: `Home.md` is this hub, `_Sidebar.md` is the navigation,
`Getting-Started.md` is the first page to read, and `Engineering-Principles.md` is the index that links
each rule to the page stating it in full. Every other content page belongs to a topic folder.

## How to read a page

Every content page follows the same shape:

| Section | Answers |
| -- | -- |
| **The rule** | What is expected. |
| **Why** | The failure the rule prevents. |
| **How it is enforced** | The mechanism that makes it true — or an explicit note that it is a review obligation. |
| **At your scale** | What to do when the repository is smaller than the one the rule came from. |

Rules a build can enforce are labelled as such. Rules only a reviewer can enforce are labelled as
review obligations. A convention that claims to be enforced when it is not is worse than no convention
at all, so the two are never blurred.

This page is navigation rather than a rule, and
[Engineering Principles](Engineering-Principles.md) is an index that links to the page stating each
rule in full.

## Get started

Read these first. They establish the vocabulary and the smallest repository that satisfies every rule.

| Page | Answers |
| -- | -- |
| [Getting Started](Getting-Started.md) | The smallest repository that satisfies every rule, in the order you would build it. |
| [Engineering Principles](Engineering-Principles.md) | The non-negotiable rules, and where the authoritative statement of each one lives. |

## Repository foundations

How a repository is laid out, what each project owns, how the architecture is enforced, and how the
documentation itself is written.

| Page | Answers |
| -- | -- |
| [Repository Shape](foundations/Repository-Shape.md) | Where a file goes, what it is called, and how a project's name becomes its namespace. |
| [Project Responsibilities](foundations/Project-Responsibilities.md) | What each project is for, how it is named, and what it may depend on. |
| [Architecture as Tests](foundations/Architecture-As-Tests.md) | How the build refuses a dependency that points the wrong way. |
| [Verification Model](foundations/Verification-Model.md) | Which checks run, in which tier, for which change. |
| [Documentation Convention](foundations/Documentation-Convention.md) | How the wiki is written, structured and published. |
| [Dependency Versions](foundations/Dependency-Versions.md) | Which version to declare — the latest for an application, the lowest compatible for a published component. |

## Data and persistence

These apply only when the repository persists data. Value objects, schemas and results cross a
boundary first; the identifier and EF Core rules then decide how that data is stored.

| Page | Answers |
| -- | -- |
| [Contracts and Data Exchange](data/Contracts-And-Data-Exchange.md) | How value objects, schemas and results cross a boundary. |
| [Well-Known Identifiers](data/Well-Known-Identifiers.md) | Identities whose value must be identical in every process and restart, derived with UUIDv5. |
| [GUID Key Strategy](data/Guid-Key-Strategy.md) | Choosing and applying a store-ordered GUID key for SQL Server or PostgreSQL. |
| [EF Core Integration](data/EF-Core-Integration.md) | How the context, providers, value objects, models and migrations fit together. |

## Testing

How tests are written, and where their data comes from.

| Page | Answers |
| -- | -- |
| [Testing and Test Data](testing/Testing-And-Test-Data.md) | How tests are written, and where their data comes from. |

## Working with agents

How a repository is made legible to a coding agent, and how shared guidance is published as a skill.

| Page | Answers |
| -- | -- |
| [Agent Enablement](agents/Agent-Enablement.md) | How a repository is made legible to a coding agent. |
| [Agent Skills Distribution](agents/Agent-Skills-Distribution.md) | How a skill is published once and reaches both a package's consumers and an agent's own directories. |
| [Agent Guidance Skills](agents/Agent-Guidance-Skills.md) | The shared architecture, telemetry and persistence guidance, and the context an agent must establish first. |

## Adoption and governance

Which parts of the playbook apply at your size, how a claim about another repository is checked, and
how a change to either the playbook or a release is made.

| Page | Answers |
| -- | -- |
| [Adopting This at Your Scale](governance/Adopting-This-At-Your-Scale.md) | Which parts apply to a small, medium or large repository. |
| [Cross-Repository Harvest](governance/Cross-Repository-Harvest.md) | Which repository owns which convention, how it is read at `main`, and what the snapshot records. |
| [Contributing](governance/Contributing.md) | How to change the playbook itself. |
| [Release Flow](governance/Release-Flow.md) | How a change becomes a version, and what a release means here. |

## The reference implementation

`src/` in this repository is a deliberately small layered repository that is built, linted and tested
by the same pipeline every Purview repository uses. It exists so the rules in this playbook are not
just prose: the dependency-direction rules fail the build when they are broken, and the test-data
conventions are shown in the form they are meant to be copied.

| Page | Answers |
| -- | -- |
| [Worked Examples](examples/Worked-Examples.md) | The reference implementation in this repository, walked end to end. |

The conventions that need EF Core live in `samples/Acme.Persistence`, a standalone runnable sample
started with `just samples`. It is outside `src/` so the reference implementation stays in-memory.
[EF Core Integration](data/EF-Core-Integration.md), [Well-Known Identifiers](data/Well-Known-Identifiers.md)
and [GUID Key Strategy](data/Guid-Key-Strategy.md) are the rules it proves.

## Provenance

Every rule here is drawn from Purview repositories and the public specifications and packages cited on
its page — chiefly
[build-sdk](https://github.com/purview-dev/build-sdk) (the MSBuild SDK that supplies the defaults),
[containers](https://github.com/purview-dev/containers) (a multi-package library with two backends),
[value-objects](https://github.com/purview-dev/value-objects), [results](https://github.com/purview-dev/results)
and [event-sourcing](https://github.com/purview-dev/event-sourcing) (data exchange at a boundary), and
[purview-dev.github.io](https://github.com/purview-dev/purview-dev.github.io) (the public catalogue,
and documentation as a published surface). Each page names the public repository, specification or
package a claim can be checked against, so a claim can always be checked against running code;
[Cross-Repository Harvest](governance/Cross-Repository-Harvest.md) is how that claim stays true.
