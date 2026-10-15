---
name: purview-architecture-testing
description: >-
  Use when adding, changing or debugging architecture tests in a .NET solution — dependency-direction
  rules, forbidden framework references, project naming checks, cycle detection — or when a boundary
  keeps being broken in review and should fail the build instead. Writes TUnit tests driven by a
  single policy definition rather than hand-maintained lists.
license: MIT
metadata:
  author: Purview-Dev
  version: "1.0"
---

# Purview architecture testing

A boundary that only review enforces is a boundary that erodes. The job of this skill is to turn a
rule a reviewer keeps repeating into a test that fails the build, **without** producing a wall of
brittle assertions that everyone disables six months later.

Prerequisite: know what the projects are *for*. See `purview-architecture`.

## 1. Only test a rule that is real

Before writing anything, answer: **has this rule been broken, or is it certain to be?**

A test for a dependency nobody would plausibly add is cost with no benefit. Good candidates:

- A direction that has already been violated once.
- A boundary that is load-bearing — Core staying free of EF Core, Composition being referenced by
  nothing.
- A rule a newcomer cannot infer from the code.

Bad candidates: anything the compiler already enforces, and style preferences dressed up as
architecture.

## 2. Define the policy once

The failure mode of architecture tests is a dependency matrix written twice — once in a wiki table
and once in assertions — which then disagree. The prose is the copy that drifts, because nothing
fails when it is wrong.

So: put the matrix in **one** machine-readable place and have the tests read it. A JSON file beside
the solution is enough; it needs no framework.

```json
{
  "responsibilities": [
    {
      "name": "Core",
      "aliases": ["Domain"],
      "mayDependOn": ["Shared"],
      "mustNotDependOn": ["Application", "Persistence", "Composition"],
      "forbiddenPackagePrefixes": ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore"]
    }
  ],
  "waivers": []
}
```

Then add a test that the documentation matches the policy, so the table in the wiki is falsifiable
too. Full worked example:
[Project Responsibilities](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Project-Responsibilities.md).

## 3. Read project files, and assemblies

Two sources, because they catch different things:

| Source | Catches | Misses |
| -- | -- | -- |
| `.csproj` `ProjectReference` | The reference a reviewer actually added | A dependency arriving transitively |
| Compiled assembly references | A dependency that arrived indirectly | Intent — it cannot tell direct from transitive |

Read the project files for the declared-intent rules (the matrix, naming, cycles) and the assemblies
for leak detection. Classify a project by the **first segment** of its name, so
`Persistence.SqlServer` and `Persistence` are both Persistence — qualification is a naming choice,
never a different rule.

## 4. The rules worth having

| Rule | What it catches |
| -- | -- |
| Dependency matrix | Core referencing Persistence; Application referencing a concrete provider |
| Nothing references Composition | A circular implementation choice |
| Sibling independence | `Providers.GitHub` reaching into `Providers.AzureDevOps` |
| Forbidden package prefixes | EF Core in Core; ASP.NET hosting in a domain project |
| Cycle detection | A graph that MSBuild rejects with an unreadable message |
| Catch-all names | `Infrastructure`, `Services`, `Helpers` appearing as project names |
| No production → test reference | A test helper leaking into shipped code |

## 5. Write failures someone can act on

This is what decides whether the suite survives. A failure must name:

1. The offending project.
2. The rule violated.
3. The specific reference that broke it.
4. The direction the dependency *should* run.
5. The likely fix.

```text
'Application' (Application) references 'Persistence.SqlServer' (Persistence), but Application
must not depend on Persistence. Invert it: Persistence may depend on Application, or introduce
an abstraction that Application owns and Persistence implements.
```

Not `Expected collection to be empty`. Collect every violation and report them as a numbered list —
one test run should show all of them, not the first.

## 6. Waivers, not suppressions

A repository that predates the policy will violate it. Deleting the test or adding a blanket
`#pragma` makes the policy a lie. Record the departure instead:

```json
{
  "project": "Infrastructure",
  "mapsTo": "Adapters",
  "rules": ["naming"],
  "reason": "Holds one adapter, predates the catalogue; renaming churns the wiki and docs for no gain.",
  "reviewedOn": "2026-10-15"
}
```

Then test the waivers themselves: each must name a project that still exists, a rule, and a
substantive reason. A waiver for a deleted project is dead weight that hides the next one.

Critically, a waiver should excuse **the narrowest thing possible** — usually the name. `mapsTo`
keeps the project classified so every dependency rule still applies to it.

## 7. Conventions

- TUnit, matching the repository's other tests. No separate architecture-testing framework or NuGet
  package unless reuse across repositories actually justifies one.
- Arrange/Act/Assert, one behaviour per test.
- Find the repository root by probing upward for a marker file — never by counting `../` from the
  test binary, which differs between a local build and CI.
- Keep the policy reader free of MSBuild and test-framework types so it can be exercised directly.

## Checks before you answer

- Is every rule you added one that has been, or will be, broken?
- Is the matrix defined once, with the documentation checked against it?
- Does each failure name the fix, not just the fact?
- Did you record a waiver rather than weaken a rule for existing code?

## See also

- `purview-architecture` — what the projects are for, and how they are named
- [Architecture as Tests](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Architecture-As-Tests.md)
