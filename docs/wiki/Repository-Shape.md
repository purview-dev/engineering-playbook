# Repository Shape

## The rule

A repository has one layout, and every file has exactly one obvious home. The shape is the same whether
the repository ships one package or twelve, because a contributor (or an agent) should be able to guess
where something lives without being told.

```text
Repository/
├── .config/                      dotnet-tools.json, lefthook.yml, purview-build.json
├── .github/
│   ├── workflows/                pr.yml, release.yml, any repository-specific workflow
│   └── copilot-instructions.md   defers to AGENTS.md
├── skills/                       skills this repository publishes (see Agent Enablement)
├── .agents/skills/               skills installed from elsewhere by the skills CLI; gitignored
├── .purview/                     SDK state; gitignored
├── docs/
│   └── wiki/                     the wiki; docs_dir in mkdocs.yml
├── src/
│   ├── <Name>.slnx               canonical solution
│   ├── Directory.Build.props     NamespacePrefix + the SDK import
│   ├── Directory.Build.targets   the SDK targets import
│   ├── src/<Project>/<Project>.csproj
│   └── tests/<TestProject>/<TestProject>.csproj
├── samples/                      runnable, consumer-shaped examples (optional)
├── templates/                    copy-paste starters for other repositories (optional)
├── .editorconfig                 bootstrapped by the SDK; committed
├── .gitattributes
├── .gitignore
├── AGENTS.md                     the repository's instructions to agents
├── commitlint.config.mts         Conventional Commits rules
├── Directory.Packages.props      Central Package Management versions
├── global.json                   SDK pin + test runner
├── Justfile                      supported local commands
├── LICENSE.md
├── mkdocs.yml                    wiki site configuration
├── nuget.config
├── package.json                  authoritative version
└── README.md
```

`artifacts/`, `.tools/` and `TestResults/` are generated and gitignored; they are in the layout above
only so that seeing them is not a surprise.

Not every file above is created by [Getting Started](Getting-Started.md), and the difference is worth
knowing before you follow it expecting a finished repository:

| File | Where it comes from |
| -- | -- |
| `.editorconfig` | Written by the SDK on the first build |
| `.gitignore`, `.gitattributes` | The SDK's own `templates/` folder, so they stay in step with it |
| `commitlint.config.mts`, `nuget.config`, `LICENSE.md`, `package.json` | Repository-level choices; not covered by Getting Started |
| Everything else | Getting Started steps 1–12, or [`templates/`](https://github.com/purview-dev/engineering-playbook/tree/main/templates) |

## Why

The layout is not aesthetics. Three specific things depend on it:

- **Test discovery.** The pipeline finds tests under `src/tests` and filters them by the category the
  project name implies. A test project placed anywhere else is not run.
- **Root discovery.** The SDK locates the repository root by asking `git`, then by probing upward for a
  `.git` marker, then by falling back to the `Directory.Build.props` directory. The first two only work if
  the layout is the one above.
- **The SDK import.** MSBuild walks up from each project to the nearest `Directory.Build.props`. If a
  stray `Directory.Build.props` appears deeper in the tree, projects beneath it silently stop inheriting
  the SDK.

## Naming: one name, derived everywhere else

A project's name is its `.csproj` filename and its parent directory name, and those two must match — a
build-time check (`PurviewProjectFileNameMismatch`) fails when they do not. From that one name:

| `.csproj` filename | `AssemblyName` / `PackageId` | `RootNamespace` | Detected as |
| -- | -- | -- | -- |
| `Domain.csproj` | `Acme.Domain` | `Acme.Domain` | source project |
| `Domain.UnitTests.csproj` | `Acme.Domain.UnitTests` | `Acme.Domain` | `IsTestProject`, `TestingType=Unit` |
| `Domain.IntegrationTests.csproj` | `Acme.Domain.IntegrationTests` | `Acme.Domain` | `IsTestProject`, `TestingType=Integration` |
| `SharedTestingInfra.csproj` | `Acme.SharedTestingInfra` | `Acme` | `IsSharedTestingProject` |

Use the **short** name. `Domain.csproj`, not `Acme.Domain.csproj` — the prefix is added for you, and
repeating it produces `Acme.Acme.Domain`.

A test project's namespace is the namespace of the type it tests, not a `.Tests` namespace. That is why
`ConventionIdTests` sits in `Acme.Domain` beside `ConventionId`: the test reads as part of the thing it
describes, and no `using` is needed to reach the type under test.

## The suffix-stripping trap

The SDK removes a set of **layer suffixes** from `RootNamespace` — `Core`, `Infrastructure` and
`Abstractions` are the ones you will meet first, alongside the `Shared*` and `SharedTesting*` project
names.

The consequence is the thing to remember: a project called `Infrastructure` has `AssemblyName`
`Acme.Infrastructure` but declares its types in namespace `Acme`, and a project called `Core` declares
its types in `Acme` too. That is deliberate: such a project *is* the root layer, and
`Acme.Core.ContainerBuilder` would be noise. [containers](https://github.com/purview-dev/containers) is
the reference — its `Core` project holds `namespace Purview.Containers;`.

This page does not reproduce the list. The SDK owns it, a copy here is a second source of truth, and the
two will drift the moment a suffix is added: see [build-sdk: Namespace
stripping](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Assembly-Name-Generation.md#namespace-stripping).

One carve-out is worth knowing because it is the one people assume the other way round: Roslyn component
suffixes — `SourceGenerators`, `Analyzers`, `CodeFixes` and their siblings — are **not** stripped. A
generator, its analyzers and its code fixes are separate assemblies that each need their own namespace.

The trap is that the strip is invisible in the project file, so the mismatch surfaces as a warning from
the IDE's folder-structure rule rather than as a build error. If you want a layer to keep its suffix,
remove it from the list **after** the import, because the item has to exist before it can be removed:

```xml
<Import Sdk="Purview.BuildSdk" Project="Sdk.props" />

<ItemGroup>
	<NamespaceRemoveSuffix Remove="Infrastructure" />
</ItemGroup>
```

Removing a suffix the SDK never declared is a no-op, so this line is safe to add and does not have to be
kept in step with the SDK's list.

## Test projects: the filename is the wiring

A test project file is usually empty, because the name carries everything:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

The detected suffix becomes the project's baseline test category, which is what the pipeline filters on —
so the tier a test belongs to is decided by where it lives, not by an attribute someone remembered to add.

**Detection is a pattern, not a list of blessed names.** The SDK matches the project name against
`^.*\.([\w|_]\w*)Tests?$` and takes the captured word as `TestingType`. `Domain.GoldenTests` is therefore
detected exactly as readily as `Domain.UnitTests`; it gets the runner, coverage and `[Category=Golden]`.
There is no set of recognised suffixes to fall foul of.

> **The silent failure is the missing target, not an unusual suffix.** The pattern requires the dot, so a
> project named plainly `UnitTests` matches nothing. It is not detected as a test project at all — no
> TUnit, no coverage, no `InternalsVisibleTo`, no `[Category]` — and the build says nothing, because a
> project that is not a test project is simply a library. Name it `<Target>.UnitTests`.

The SDK does ship an explicit list of roughly fifty test types, but it governs **`InternalsVisibleTo`
generation**, not detection: a non-test project grants access to `{AssemblyName}.{TestType}Tests` for each
name on that list. A suffix outside the list still builds, still runs and still gets its category — it
just cannot reach the target project's `internal` members. See [build-sdk:
InternalsVisibleTo](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/InternalsVisibleTo.md).

Three consequences worth knowing:

- **The target project is inferred.** `Domain.UnitTests` gets a `ProjectReference` to `../Domain/`,
  `../../Domain/`, `../src/Domain/` or `../../src/Domain/` — whichever exists, and nothing at all if none
  does. That is why `Architecture.UnitTests`, which has no `Architecture` project, builds: it declares its
  references explicitly instead.
- **`InternalsVisibleTo` is generated.** Every non-test project grants access to
  `{AssemblyName}.{TestType}Tests` for each known test type, plus every shared testing project. A test can
  reach `internal` members of the project it targets without the project declaring anything.
- **A shared testing project stays a library.** Names like `SharedTestingInfra`,
  `SharedTestingFramework`, `SharedTestingUtilities` get the test packages but not the runner, so the
  assembly is referenced rather than executed.

## `src/src` and `src/tests`

For a repository with more than a handful of projects, source and tests are separated:

```text
src/
├── <Name>.slnx
├── Directory.Build.props
├── Directory.Build.targets
├── src/          production projects
└── tests/        test and shared-testing projects
```

The doubling looks odd for a week and then stops mattering, because `src/src/...` in a path is unambiguous
and `src/tests/...` matches the `TestRoot` the pipeline expects. Smaller repositories put projects side by
side instead, which works identically — the SDK detects a test project by name, not by folder. See
[build-sdk: Project Naming
Conventions](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Project-Naming-Conventions.md).

## Where the SDK import lives

Either at the repository root or in `src/`. This playbook and the multi-project Purview repositories put
`Directory.Build.props` and `Directory.Build.targets` in `src/`, which keeps the root for repository-level
configuration only. The SDK's root discovery is unaffected, because it asks `git` rather than counting
directories.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| `.csproj` filename matches its parent directory | The build (`PurviewProjectFileNameMismatch`) |
| `AssemblyName` and `RootNamespace` follow the project name | The build (SDK derivation) |
| A test project is discovered and categorised | The build (`TestingType` detection) and the pipeline's filter |
| The solution is the one the pipeline builds | Review, plus `Build:Solution` in `purview-build.json` |
| Everything else on this page | Review |

## At your scale

- **One project.** `src/src/<Project>` plus one `src/tests/<Project>.UnitTests`. Skip `samples/` and
  `templates/`.
- **A library with a few modules.** Add `src/src/<Module>` per module, one test project per module, and one
  shared testing project. Add `samples/` once the README's getting-started code is long enough that it has
  stopped compiling.
- **Several packages.** Add an `Sdk/` folder per packable project for package-only assets (`README.md`,
  `buildTransitive/**`), and `templates/` once another repository has started copying your files by hand.

## See also

- [Getting Started](Getting-Started.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [Documentation Convention](Documentation-Convention.md)
- [Agent Enablement](Agent-Enablement.md)
