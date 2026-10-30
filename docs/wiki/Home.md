# Engineering Playbook

This is the playbook for building and maintaining a Purview repository. It records the conventions the
projects under `purview-dev` are held to: how a repository is laid out, how its architecture is enforced
by tests rather than by review, how a change is verified in tiers, how data crosses a boundary, how
tests are written and fed, and how a repository is made legible to a coding agent.

The conventions were previously discoverable only by reading the repositories and inferring the pattern
from the repetition. They are stated here once, with the evidence and the trade-offs attached, and
scaled so that a repository adopting them can take the part that fits.

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

This page and [`index.md`](index.md) are navigation rather than rules, and
[Engineering Principles](Engineering-Principles.md) is an index that links to the page stating each
rule in full.

## The playbook

| Page | Covers |
| -- | -- |
| [Getting Started](Getting-Started.md) | The smallest repository that satisfies every rule, in the order you would build it. |
| [Engineering Principles](Engineering-Principles.md) | The non-negotiable rules, and where the authoritative statement of each one lives. |
| [Repository Shape](Repository-Shape.md) | Where a file goes, what it is called, and how a project's name becomes its namespace. |
| [Project Responsibilities](Project-Responsibilities.md) | What each project is for, how it is named, and what it may depend on. |
| [Architecture as Tests](Architecture-As-Tests.md) | How the build refuses a dependency that points the wrong way. |
| [Verification Model](Verification-Model.md) | Which checks run, in which tier, for which change. |
| [Contracts and Data Exchange](Contracts-And-Data-Exchange.md) | How value objects, schemas and results cross a boundary. |
| [Well-Known Identifiers](Well-Known-Identifiers.md) | Identities whose value must be identical in every process and restart, derived with UUIDv5. |
| [GUID Key Strategy](Guid-Key-Strategy.md) | Choosing and applying a store-ordered GUID key for SQL Server or PostgreSQL. |
| [Testing and Test Data](Testing-And-Test-Data.md) | How tests are written, and where their data comes from. |
| [Agent Enablement](Agent-Enablement.md) | How a repository is made legible to a coding agent. |
| [Agent Skills Distribution](Agent-Skills-Distribution.md) | How a skill is published once and reaches both a package's consumers and an agent's own directories. |
| [Agent Guidance Skills](Agent-Guidance-Skills.md) | The shared architecture, telemetry and persistence guidance, and the context an agent must establish first. |
| [Documentation Convention](Documentation-Convention.md) | How the wiki is written, structured and published. |
| [Worked Examples](Worked-Examples.md) | The reference implementation in this repository, walked end to end. |
| [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md) | Which parts apply to a small, medium or large repository. |
| [Contributing](Contributing.md) | How to change the playbook itself. |
| [Release Flow](Release-Flow.md) | How a change becomes a version, and what a release means here. |

## The reference implementation

`src/` in this repository is a deliberately small layered repository that is built, linted and tested
by the same pipeline every Purview repository uses. It exists so the rules in this playbook are not
just prose: the dependency-direction rules fail the build when they are broken, and the test-data
conventions are shown in the form they are meant to be copied. [Worked
Examples](Worked-Examples.md) walks it end to end.

The conventions that need EF Core live in `samples/Acme.Persistence`, a standalone runnable sample
started with `just samples`. It is outside `src/` so the reference implementation stays in-memory.

## Provenance

Every rule here is drawn from Purview repositories and the public specifications and packages cited on
its page — chiefly
[build-sdk](https://github.com/purview-dev/build-sdk) (the MSBuild SDK that supplies the defaults),
[containers](https://github.com/purview-dev/containers) (a multi-package library with two backends),
[value-objects](https://github.com/purview-dev/value-objects), [results](https://github.com/purview-dev/results)
and [event-sourcing](https://github.com/purview-dev/event-sourcing) (data exchange at a boundary), and
[purview-dev](https://github.com/purview-dev/purview-dev) (documentation as a published surface). Each
page names the public repository, specification or package a claim can be checked against.
