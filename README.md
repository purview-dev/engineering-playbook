# Engineering Playbook

The reusable engineering conventions behind the Purview projects — how a repository is shaped, how
architecture is held by tests, how a change is verified in tiers, how data crosses a boundary, and how
coding agents are enabled to work in a codebase without hand-holding.

> **Documentation-first.** This repository ships no library and no NuGet package. It contains the
> conventions themselves, ready-to-copy templates, and a small in-repo reference implementation whose
> only job is to prove the conventions compile, run, and fail when they should.

## What's inside

| Area | Question it answers |
| -- | -- |
| [Getting started](docs/wiki/Getting-Started.md) | What is the smallest repository that satisfies every rule, in the order you build it? |
| [Engineering principles](docs/wiki/Engineering-Principles.md) | What are the non-negotiable rules, and where is each one stated in full? |
| [Repository shape](docs/wiki/Repository-Shape.md) | Where does a file go, and what is it called? |
| [Project responsibilities](docs/wiki/Project-Responsibilities.md) | What is each project for, how is it named, and what may it depend on? |
| [Architecture as tests](docs/wiki/Architecture-As-Tests.md) | How does the build refuse a dependency that points the wrong way? |
| [Verification model](docs/wiki/Verification-Model.md) | Which checks run, in which tier, for which change? |
| [Contracts and data exchange](docs/wiki/Contracts-And-Data-Exchange.md) | How do value objects, schemas and results cross a boundary? |
| [Well-known identifiers](docs/wiki/Well-Known-Identifiers.md) | How is an identity derived so every process and restart agrees? |
| [GUID key strategy](docs/wiki/Guid-Key-Strategy.md) | How is a sequential GUID key chosen and applied per store? |
| [EF Core integration](docs/wiki/EF-Core-Integration.md) | How do the context, providers, value objects, models and migrations fit together? |
| [Testing and test data](docs/wiki/Testing-And-Test-Data.md) | How are tests written, and where does their data come from? |
| [Agent enablement](docs/wiki/Agent-Enablement.md) | How is a repository made legible to a coding agent? |
| [Agent skills distribution](docs/wiki/Agent-Skills-Distribution.md) | How is a skill published once and reach an agent? |
| [Agent guidance skills](docs/wiki/Agent-Guidance-Skills.md) | Which shared guidance ships as a skill, and what context comes first? |
| [Documentation convention](docs/wiki/Documentation-Convention.md) | How is the wiki written, structured and published? |
| [Worked examples](docs/wiki/Worked-Examples.md) | What does the reference implementation under `src/` actually demonstrate? |
| [Adopting this at your scale](docs/wiki/Adopting-This-At-Your-Scale.md) | Which parts apply to a small, medium or large repository? |
| [Contributing](docs/wiki/Contributing.md) | How do I change the playbook itself? |
| [Release flow](docs/wiki/Release-Flow.md) | How does a change become a version, and what does a release mean here? |

## Start here

- **New to the playbook?** [Getting started](docs/wiki/Getting-Started.md) walks the smallest
  repository that satisfies every rule.
- **Adopting it?** [Adopting this at your scale](docs/wiki/Adopting-This-At-Your-Scale.md) picks the
  subset that fits, and [`templates/`](templates) is the copy-paste starting point.
- **Want to see it run?** [Worked examples](docs/wiki/Worked-Examples.md) walks the reference
  implementation under [`src/`](src), which this repository's own pipeline builds and tests.

## Repository layout

| Path | Purpose |
| -- | -- |
| `docs/wiki` | The playbook. Aggregated by the purview-dev website. |
| `templates/` | Ready-to-copy starter files for a repository adopting the playbook. |
| `src/` | The reference implementation: a layered repository whose architecture rules are tests. |
| `samples/` | Runnable samples for conventions that `src/` cannot host; run with `just samples`. |
| `mkdocs.yml` | Wiki site configuration (`docs_dir: docs/wiki`). |
| `.config/purview-build.json` | Pipeline configuration for the shared Purview build workflow. |
| `Justfile` | Supported local workflow commands. |
| `package.json` | Authoritative version for the repository. |

## Local workflow

```text
just build              # dotnet build (Debug)
just test               # run the unit tests
just lint-check         # CSharpier check
just samples            # build and run the standalone persistence sample
just pipeline-pr        # restore, build, lint, test through the shared Purview.Build tool
just scrub              # reset bin/obj, forced restore, build-server shutdown
```

## License

MIT.
