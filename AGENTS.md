# Agent Instructions

## Purpose and authority

This repository contains the **Engineering Playbook** — the documentation of the reusable engineering
conventions the Purview projects are built on. It is documentation-first: it ships no library and no NuGet
package. It holds the conventions themselves, ready-to-copy templates, and a small in-repo reference
implementation whose only job is to prove the conventions compile, run, and fail when they should.

- This file is the repository-wide source of truth for AI agents. A more-specific `AGENTS.md` in a subtree,
  if one is ever added, takes precedence for that subtree.
- `.github/copilot-instructions.md` defers to this file.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then
  established code patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles, local
  configuration, test output, or registry credentials.

## Source of truth and layout

| Path | Purpose |
| -- | -- |
| `docs/wiki` | The playbook itself. `Home.md` is the hub and `_Sidebar.md` is the navigation. |
| `mkdocs.yml` | Wiki site configuration (`docs_dir: docs/wiki`) |
| `src/EngineeringPlaybook.slnx` | Canonical solution for restore, build, lint and test |
| `src/src/Domain` | The reference implementation's value objects and aggregate |
| `src/src/Application` | The port (`IConventionRepository`) and the service that owns the rules |
| `src/src/Infrastructure` | The in-memory adapter that implements the port |
| `src/tests/Domain.UnitTests` | Value-object behaviour; the value-object exception to `TestBuilder` |
| `src/tests/Application.UnitTests` | The service, against a TUnit.Mocks substitute |
| `src/tests/Architecture.UnitTests` | Dependency-direction rules; a wrong `ProjectReference` fails the build |
| `src/tests/SharedTestingInfra` | `TestBuilder`; referenced automatically by every test project |
| `templates/` | Ready-to-copy starter files for a repository adopting the playbook |
| `src/Directory.Build.props` | `NamespacePrefix`, the SDK import, and `NamespaceRemoveSuffix` |
| `.config/purview-build.json` | Pipeline configuration: solution, test discovery, filter |
| `Justfile` | Supported local commands |
| `package.json` | Authoritative version; the release reads it and nothing else |

## The two jobs, and why they conflict

This repository has two obligations that pull in opposite directions, and every change has to satisfy both:

1. **The documentation is the product.** A rule must be stated once, with the failure it prevents, the
   mechanism that makes it true, and what a smaller repository does instead. A rule that claims to be
   enforced when it is not is worse than no rule.
2. **`src/` must follow the rules it documents.** A change to `src/` that breaks a documented convention is a
   bug even when the build passes, because the reference implementation is the evidence.

So: when a rule changes, the page and `src/` change together, or the page says plainly why `src/` does not
demonstrate it.

## Documentation conventions

- **One page per concern**, in `docs/wiki/<Page-Name>.md`. Title case, hyphens between words.
- **Four sections, in order:** the rule, why, how it is enforced, at your scale. A rule a build cannot check
  is labelled a review obligation — never described as enforced.
- **A new page goes in `_Sidebar.md` and in the table in `Home.md`.** A page in neither does not exist.
- **Link to the authority rather than paraphrasing it.** The SDK's own wiki is authoritative on the SDK's
  behaviour; a paraphrase here is a second source of truth that will drift.
- **Name the public repository a rule came from.** Every rule came from a real repository and a real failure.
- **No reference to a private repository or product, ever.** This playbook is public, and it is aggregated by
  a public site. A rule that can only be justified by something private cannot be reviewed by the people it
  is published to.
- **Fences always declare a language.** Mermaid is not enabled in `mkdocs.yml`, so raw Mermaid source goes in
  a ` ```text ` block.

## Code conventions

`src/` follows the same conventions every Purview repository follows. The ones that bite here specifically:

- **`var` for every initialisation except a direct `new()`.** The SDK's `.editorconfig` sets
  `csharp_style_var_elsewhere = true:warning`, so an explicit type on a declaration initialised from a method
  call is a warning. The carve-out is object creation, where `PDS0003` prefers an explicit type with
  target-typed `new()`.
- **A private field has no access modifier** (`omit_if_default`), but **an interface implementation keeps
  `public`**, because `public` is not the default there.
- **`Infrastructure` keeps its own namespace** only because `src/Directory.Build.props` removes that suffix
  from the SDK's `NamespaceRemoveSuffix` list. Without that line the SDK folds the types into
  `Purview.EngineeringPlaybook`.
- **Factories on `TestBuilder` are flat, not nested.** `CA1034` is an `error` in the SDK's style policy and
  `PRSGD0008` rejects adding it to `NoWarn`.
- **A test class lives in the namespace of the class under test**, and is named `{Class}Tests`.
- **A family's package version is a property.** In `Directory.Packages.props`, a version shared by a family
  of packages — `TUnit`/`TUnit.Core`/`TUnit.Mocks` here — is declared once in a labelled `PropertyGroup` and
  referenced as `Version="$(TUnitVersion)"`, so a bump cannot be applied to half the family. A package with
  no family, like `Bogus`, keeps its literal version inline.
- **A clean build has no warnings.**

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
`commitlint.config.mts`. A documentation-only change is `docs:`; a change to the reference implementation is
`feat:` or `fix:`.

## Completion checklist

Before handing work back:

- Confirm the requested behaviour and scope are satisfied, and that only intended files changed.
- If a rule was added or changed: the page states the failure it prevents, says whether it is enforced or a
  review obligation, has an "at your scale" section, is in `_Sidebar.md` and the `Home.md` table, and is
  demonstrated in `src/` — or the page says why it is not.
- If `src/` changed: the build is still warning-free, the architecture rules still pass, and the pages that
  describe the changed code are still accurate.
- Run the checks in proportion to the risk, and state exactly what ran. If a check was skipped or blocked,
  give the concrete reason and the remaining risk.
