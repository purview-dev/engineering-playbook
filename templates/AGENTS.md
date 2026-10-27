# Agent Instructions

<!--
	COPY TO: AGENTS.md  (repository root)

	Replace every `Acme` and every `<...>` placeholder. Delete any section that does not apply — an
	AGENTS.md with a section that describes nothing is worse than a short one, because it teaches the
	reader that the file is not maintained.

	Keep it specific. "Follow established patterns" is not guidance; "domain content in a test comes from
	TestBuilder; never construct a domain type inline" is guidance. Keep it short: this is a table of
	contents, and the wiki is where the long version lives.
-->

## Purpose and authority

This repository contains `Acme.<Product>`, <one sentence on what it is and who uses it>.

- This file is the repository-wide source of truth for AI agents. A more-specific `AGENTS.md` in a
  subtree, if one is ever added, takes precedence for that subtree.
- `.github/copilot-instructions.md` defers to this file.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then
  established code patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles,
  local configuration, test output, or registry credentials.

## Source of truth and layout

| Path | Purpose |
| -- | -- |
| `src/Acme.slnx` | Canonical solution for restore, build, test and pack |
| `src/src/<Project>` | <what lives in each project, one row per project> |
| `src/tests/<Project>.UnitTests` | Unit tests for that project; the class under test's namespace is the test's namespace |
| `src/tests/SharedTestingInfra` | `TestBuilder` and shared fixtures; referenced by every test project |
| `src/tests/Architecture.UnitTests` | Dependency-direction rules; a wrong `ProjectReference` fails the build |
| `docs/wiki` | User-facing documentation; `_Sidebar.md` is the navigation |
| `.config/purview-build.json` | Pipeline configuration: solution, test discovery, filter |
| `Justfile` | Supported local commands |
| `src/Directory.Build.props` | `NamespacePrefix` and the SDK import |
| `package.json` | Authoritative version; the release reads it and nothing else |

## Conventions

- **Identity is derived.** A project's name is its folder and `.csproj` filename, and those must match.
  `RootNamespace` and `AssemblyName` follow from `NamespacePrefix`; never type a namespace prefix by hand.
- **A project file is short.** The SDK supplies the target framework, language version, nullable context,
  analyzers and assembly identity. Add to a project file only when you are overriding a default.
- **A test project file is empty.** The filename does the wiring: `*.UnitTests` gets the runner, the
  category and the `ProjectReference` to the project it targets.
- **Tests.** TUnit, and TUnit.Mocks for substitutes. No other test or substitution framework. Explicit
  `// Arrange`, `// Act`, `// Assert`. Method names are `{Subject}_{Scenario}_{Expectation}` and `async Task`.
  One `{Class}Tests` class per class under test, in the namespace of the class under test.
- **Test data comes from `TestBuilder`.** Never construct a domain type inline in a test. Override only the
  value the test is proving. The exception is a test proving a type's own `Create`/`Hydrate` behaviour,
  which must name the rejected input itself.
- **Acceptance tests use BDTest over TUnit** (`BDTest.TUnit`), never Gherkin, SpecFlow or Reqnroll — the
  adapter keeps TUnit as the one runner, and a code-only scenario has no feature file to drift from its
  steps. They live in `<Target>.AcceptanceTests`, where Given/When/Then replaces the AAA comments, and the
  chain must end in `.BDTestAsync()` — a chain that stops at `.Then(...)` passes without running a step.
- **Containers come from `Purview.Containers`, not `Testcontainers` directly.** One API over Microsoft WSL
  Containers (no Docker installation on Windows) or Docker, with typed modules for SQL Server, PostgreSQL,
  Redis, MySQL, RabbitMQ, Azurite and NATS. It is experimental and prerelease, so pin the version you adopt.
  Put the suite in its own `*.IntegrationTests` project so the tier comes from the name. Locally, probe
  with `ContainerBackends.ProbeAllAsync()` and skip when nothing is usable — a missing runtime must be a
  skip, never a pass. In CI, pin the backend with `PURVIEW_CONTAINERS_BACKEND` so an unavailable runtime
  fails the job instead of skipping every test and reporting green.
- **Package versions are central, and a family's version is a property.** All versions live in
  `Directory.Packages.props`. A version shared by a family — `TUnit`/`TUnit.Core`/`TUnit.Mocks`, an EF Core
  provider against EF Core, any `Purview.*` product released as a set — is declared once in a labelled
  `PropertyGroup` and referenced as `Version="$(TUnitVersion)"`, so a bump cannot be applied to half the
  family. A package with no family keeps its literal version inline.
- **In a test, prefer the async overload wherever the API offers one** — `File.ReadAllTextAsync`, not
  `File.ReadAllText` — **and that reaches through test helpers.** A helper calling such an API is converted
  to `async Task`, gains the `Async` suffix and a trailing `CancellationToken`; a property that hides IO
  becomes a method, because a property cannot be `async`. `VSTHRD103` fails the build for a sync call
  inside a `Task`-returning method, but a sync helper is not `Task`-returning, so the analyzer cannot see
  it — that is the case a reviewer has to catch. Exempt only when no async overload exists
  (`File.Exists`, `File.Delete`, `Directory.EnumerateFiles`) or a base type fixes the signature; say which.
  `Task.Run`, `Task.FromResult` and `.GetAwaiter().GetResult()` are not conversions.
- **Pass a `CancellationToken`** wherever a downstream method accepts one. In a test that means: if any
  method in the call chain accepts one, the test method's **last parameter** is a `CancellationToken` —
  TUnit supplies it, and with `[Arguments]` the data binds to the leading parameters — and it is forwarded
  to every call that accepts one. Never `CancellationToken.None`, never `default`, never a token the test
  creates. One hop that drops it makes the whole chain uncancellable.
- **The build is the authority.** A convention the build checks is checked by the build; a convention only a
  reviewer can check is stated as a review obligation and never described as enforced.
- **A clean build has no warnings.** Fix it, or suppress it where it occurs with a stated justification.

## Local commands

```text
just build              # dotnet build (Debug)
just test               # dotnet test
just lint-check         # CSharpier check
just lint-fix           # CSharpier format
just pipeline-pr        # restore, build, lint, test through the shared Purview.Build tool
just scrub              # reset bin/obj, forced restore, build-server shutdown
```

Commit messages follow Conventional Commits, enforced by the `commit-msg` hook. Allowed types are in
`commitlint.config.mts`.

## Completion checklist

Before handing work back:

- Confirm the requested behaviour and scope are satisfied, and that only intended files changed.
- Update the affected documentation: `docs/wiki`, its `_Sidebar.md` and `Home.md` table for a new page, and
  `README.md` and `AGENTS.md` when the change affects them.
- Run the checks in proportion to the risk, and state exactly what ran. If a check was skipped or blocked,
  give the concrete reason and the remaining risk.
