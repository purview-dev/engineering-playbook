# Dependency Versions

## The rule

Two audiences, and the two rules point in opposite directions.

| You are publishing | Declare | Because |
| -- | -- | -- |
| an application, a test suite or a tool — something that runs, and nobody installs | the **latest** version compatible with your runtime | you own the runtime, and a version you do not take is a fix you did not get |
| a component — a NuGet or npm package that somebody else installs | the **lowest** version that compiles, passes its tests and satisfies the contract you document, per target framework | a consumer's resolution is a set of lower bounds, so your floor becomes their floor |

The second row is the golden rule of package development, and it is the one that is broken by accident
rather than by decision: the version you happened to have installed is never the one you should declare.

**A page never states a release number.** A version in prose is wrong within one release, and the cost is
a reader pinning something three releases old because a page they trusted said so. Where a current
version is genuinely useful, show a badge: it is generated from the registry, so it cannot drift.

| Component | Registry | Current |
| -- | -- | -- |
| `Purview.BuildSdk` | NuGet | ![Purview.BuildSdk](https://img.shields.io/nuget/v/Purview.BuildSdk) |
| `Purview.Build` (tool) | NuGet | ![Purview.Build](https://img.shields.io/nuget/v/Purview.Build) |
| `Purview.Skills.Testing` | NuGet | ![Purview.Skills.Testing](https://img.shields.io/nuget/v/Purview.Skills.Testing) |
| `Purview.Architecture.Testing` | NuGet | ![Purview.Architecture.Testing](https://img.shields.io/nuget/v/Purview.Architecture.Testing) |
| `Purview.Telemetry.SourceGenerator` | NuGet | ![Purview.Telemetry.SourceGenerator](https://img.shields.io/nuget/v/Purview.Telemetry.SourceGenerator) |
| `Purview.Containers` | NuGet | ![Purview.Containers](https://img.shields.io/nuget/vpre/Purview.Containers) |
| `TUnit` | NuGet | ![TUnit](https://img.shields.io/nuget/v/TUnit) |
| `Bogus` | NuGet | ![Bogus](https://img.shields.io/nuget/v/Bogus) |
| `Microsoft.SourceLink.GitHub` | NuGet | ![Microsoft.SourceLink.GitHub](https://img.shields.io/nuget/v/Microsoft.SourceLink.GitHub) |
| `Microsoft.Extensions.Telemetry.Abstractions` | NuGet | ![Microsoft.Extensions.Telemetry.Abstractions](https://img.shields.io/nuget/v/Microsoft.Extensions.Telemetry.Abstractions) |
| `@purview-dev/astroc4` | npm | ![astroc4](https://img.shields.io/npm/v/@purview-dev/astroc4) |

A badge is not a recommendation to move. It answers "what is current?"; which version *you* declare is
the decision the two rules above make.

## Why

**A documented version rots at the speed of the release.** This playbook told a reader to pin
`Purview.BuildSdk` 1.0.6 in `global.json` while the repository the page documents had already moved to
1.2.0 — accurate when it was written, wrong within a release, and nothing in the build could tell. The
badge replaces the claim with a lookup, so the failure cannot recur.

**A floor is a floor you cannot take back.** NuGet and npm resolve a graph to the **highest** minimum
among all constraints. A library that declares `Testcontainers` 4.16 because 4.16 is what its author had
installed forces every consumer onto 4.16 — including the consumer whose own constraint is 4.0 and whose
runtime is pinned to it. The consumer cannot refuse an upgrade the library never needed to ask for. The
declared floor is a promise about how little you require, and raising it is a breaking change wearing a
minor version number.

**The floor is per target framework.** `Purview.ValueObjects` targets `net8.0` through `net11.0`;
`Purview.Skills.Testing` is `net10.0` only. A multi-targeting component has a different floor on each
framework, and the one that matters is the oldest framework it still claims to support — not the newest
one it builds against.

**The floor is not what you test with.** A repository's own build should resolve the latest, because that
is what will be installed alongside it. The floor needs its own leg, or the first person to discover it
is a consumer.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| An application takes the latest compatible version | Review, with `dotnet list package --outdated` as the evidence |
| A component declares the lowest compatible version, per target framework | A lowest-compatible CI leg that restores against the floor and runs the suite |
| The floor still satisfies every declared target framework | `dotnet build` on each framework, and an API-compatibility check against the previous release |
| No release number is stated in the documentation | A badge, or a link to the registry — never a number in prose |
| A pin that must exist lives where a tool can check it | `global.json`, `Directory.Packages.props`, `.config/dotnet-tools.json` — the only place a number belongs |

`global.json` and a tool manifest **must** name a version; that is a pin rather than a claim, and it is
the one place a number is correct. Everything a reader copies from it is a starting point, not a target.

Deciding and checking a floor is a workflow rather than a paragraph, because it means walking a component
hierarchy: which package depends on which, at what minimum, on which framework. That workflow is the
`purview-component-compatibility` skill, and it ships a script that reads the dependency graph out of the
registry instead of trusting a table.

## At your scale

- **One application.** Take the latest compatible version and let `--outdated` tell you what you missed.
  Badges are enough for the documentation.
- **One library.** The golden rule applies from the first release. One floor, one CI leg, and the floor
  written down in the README as a badge beside the package badge.
- **A family of packages.** Keep the family on one version property so a bump cannot be applied to half of
  it, and give the floor its own job rather than a checkbox on the ordinary one.
- **A platform.** The floor is a matrix: every supported target framework, and every component that
  declares a dependency on another. `purview-component-compatibility` exists for this row.

## See also

- [Testing and Test Data](../testing/Testing-And-Test-Data.md) — the tiers a floor check belongs to
- [Verification Model](Verification-Model.md) — which check runs in which tier
- [Cross-Repository Harvest](../governance/Cross-Repository-Harvest.md) — how a component's real floor is
  read from the organisation rather than remembered

