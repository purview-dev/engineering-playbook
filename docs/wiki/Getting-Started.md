# Getting Started

## The rule

Build the smallest repository that satisfies every rule in the playbook, in the order you would
actually create the files. Each step says what it buys you, so you can stop when you have taken what
you need. The finished result is a repository whose defaults come from one MSBuild SDK, whose test
projects are wired by their filenames, whose architecture is asserted by a test, and whose
documentation is a wiki the build already knows how to find.

## Why

A convention adopted by copying somebody else's finished repository is a convention you have not read.
Building the repository in the order the rules depend on each other means every file has a reason by
the time you write it, and the build tells you immediately when a step is wrong. The order is not
arbitrary: the SDK import must come before anything that derives from it, the package versions must
exist before the first project restores, and the solution must exist before the pipeline can be pointed
at it.

## The steps

### Step 1 — Pin the SDK in `global.json`

There are **two** versions in play here, and the file pins both:

```json
{
	"sdk": {
		"version": "10.0.401",
		"rollForward": "disable",
		"allowPrerelease": false
	},
	"msbuild-sdks": {
		"Purview.BuildSdk": "1.0.6"
	},
	"test": {
		"runner": "Microsoft.Testing.Platform"
	}
}
```

`sdk.version` pins the **.NET SDK** — the `dotnet` that runs the build. `rollForward: "disable"` means
exactly that version and nothing else, so a machine with several SDKs installed cannot pick a different
one. `msbuild-sdks` pins **`Purview.BuildSdk`**, the MSBuild SDK that supplies the conventions: it is what
makes `<Import Sdk="Purview.BuildSdk" Project="Sdk.props" />` resolvable, because MSBuild restores it as a
NuGet package and reads its `.props`/`.targets`. `test.runner` selects the Microsoft Testing Platform,
which is the runner TUnit expects.

The SDK writes this file for you when it is missing, but what it writes is only the `msbuild-sdks` and
`test` blocks — **it does not write an `sdk` block at all**, so there is no `sdk.version` and no
`rollForward`. Until you add those two lines by hand, a machine with several .NET SDKs installed resolves
latest-wins, and the build that passes locally is not necessarily the build CI runs. See [build-sdk:
`global.json`
bootstrapping](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Repository-Bootstrap.md#globaljson-bootstrapping).

### Step 2 — Declare the versions the SDK injects, in `Directory.Packages.props`

Central Package Management is on by default, so every `PackageReference` the SDK adds has to have a
matching `PackageVersion` or restore fails with `NU1010`. The versions below are the ones this
repository builds with today — check them against the SDK's current release before you commit, as
[`templates/README.md`](https://github.com/purview-dev/engineering-playbook/blob/main/templates/README.md)
says.

```xml
<Project>
	<PropertyGroup>
		<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
	</PropertyGroup>

	<!-- Version Definitions -->
	<PropertyGroup Label=".NET Versions">
		<MSExtensionVersion>10.10.0</MSExtensionVersion>
	</PropertyGroup>
	<PropertyGroup Label="Purview Versions">
		<PurviewTelemetryVersion>5.0.4</PurviewTelemetryVersion>
	</PropertyGroup>
	<PropertyGroup Label="Testing Versions">
		<TUnitVersion>1.75.0</TUnitVersion>
	</PropertyGroup>

	<!-- Packages -->
	<ItemGroup Label="Purview">
		<PackageVersion Include="Purview.Telemetry.SourceGenerator" Version="$(PurviewTelemetryVersion)" />
	</ItemGroup>
	<ItemGroup Label="Microsoft Extensions">
		<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="$(MSExtensionVersion)" />
	</ItemGroup>
	<ItemGroup Label="Other">
		<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.402" />
	</ItemGroup>
	<ItemGroup Label="Testing">
		<PackageVersion Include="TUnit" Version="$(TUnitVersion)" />
		<PackageVersion Include="TUnit.Core" Version="$(TUnitVersion)" />
		<PackageVersion Include="TUnit.Mocks" Version="$(TUnitVersion)" />
		<PackageVersion Include="Bogus" Version="35.6.5" />
	</ItemGroup>
</Project>
```

`Purview.Telemetry.SourceGenerator`, `Microsoft.Extensions.Telemetry.Abstractions` and
`Microsoft.SourceLink.GitHub` are injected by the SDK itself — telemetry and SourceLink are on by
default. The rest are the test stack: TUnit is the framework, TUnit.Mocks the substitute library, Bogus
the test-data generator. See [Testing and Test Data](Testing-And-Test-Data.md).

### Versions are properties, packages are items

The shape above is the convention, and it is worth following from the first file rather than retrofitting
later: **a version shared by a family of packages is declared once as a property, and every
`PackageVersion` in that family references it.**

The failure it prevents is the partial bump. `TUnit`, `TUnit.Core` and `TUnit.Mocks` ship together and
must move together; bump two of the three by hand and restore still succeeds, because NuGet has no idea
they are related. What you get is a mismatch that surfaces at run time, in a test run, as something that
looks like a product bug. With one `$(TUnitVersion)` there is no half-done upgrade to miss in review — the
family moves, or it does not. The same applies to an EF Core provider against EF Core, an Aspire client
against its hosting package, and every `Purview.*` product whose packages are released as a set.

Two rules keep it from becoming indirection for its own sake:

- **Declare a property when the version governs a family**, or when the packages belong to a product that
  releases them together — even where you reference only one of them today, because the property is where
  the second one will look. A package with no family, like `Bogus` or `Microsoft.SourceLink.GitHub`, keeps
  its literal version inline.
- **Group with `Label`.** The file then reads as a short list of decisions — .NET, Purview, testing,
  everything else — rather than one long alphabetical block in which a stale version is invisible.

This is a convention, not something the build checks: nothing fails if you inline every version. It is on
the reviewer, and it is cheap to keep because the file is read far more often than it is written.

### Step 3 — Import the SDK, and name the namespace prefix

`src/Directory.Build.props`:

```xml
<Project>
	<PropertyGroup>
		<NamespacePrefix>Acme</NamespacePrefix>
	</PropertyGroup>

	<Import Sdk="Purview.BuildSdk" Project="Sdk.props" />
</Project>
```

`src/Directory.Build.targets`:

```xml
<Project>
	<Import Sdk="Purview.BuildSdk" Project="Sdk.targets" />
</Project>
```

`NamespacePrefix` must be set **before** the import, because the SDK derives `RootNamespace` from it
during `.props` evaluation. From this point every project beneath `src/` gets the target framework,
language version, nullable context, analyzers, code-style enforcement and assembly identity without
declaring any of them. See [Repository Shape](Repository-Shape.md).

> **Where the file goes.** MSBuild walks up from each project to the nearest `Directory.Build.props`, so
> the import can sit at the repository root or at `src/`. This repository and the multi-project Purview
> repositories put it at `src/`, which keeps the root for repository-level configuration only. The SDK's
> own repository-root discovery still resolves correctly from there, because it asks `git`.

### Step 4 — Add the solution

`src/Acme.slnx`:

```xml
<Solution>
	<Folder Name="/Solution Items/">
		<File Path="../.editorconfig" />
		<File Path="../Directory.Packages.props" />
		<File Path="../global.json" />
		<File Path="../.config/purview-build.json" />
		<File Path="Directory.Build.props" />
		<File Path="Directory.Build.targets" />
	</Folder>
	<Folder Name="/src/">
		<Project Path="src/Domain/Domain.csproj" />
	</Folder>

	<Folder Name="/tests/">
		<Project Path="tests/Domain.UnitTests/Domain.UnitTests.csproj" />
		<Project Path="tests/SharedTestingInfra/SharedTestingInfra.csproj" />
	</Folder>
</Solution>
```

The `Solution Items` folder is the list of files a reader of the solution needs and that a build would
otherwise not surface. `.slnx` is the XML solution format; `.gitattributes` pins it to LF.

### Step 5 — Create a project with a two-line project file

`src/src/Domain/Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

That is the whole file. The folder name `Domain` becomes the project name, and
`$(NamespacePrefix).Domain` becomes both `RootNamespace` and `AssemblyName`. Add a type:

```csharp
namespace Acme.Domain;

public sealed record CustomerId
{
	CustomerId(string value) => Value = value;

	public string Value { get; }

	public static CustomerId Create(string value) =>
		string.IsNullOrWhiteSpace(value)
			? throw new ArgumentException("A customer id cannot be empty.", nameof(value))
			: new CustomerId(value);

	public static CustomerId Hydrate(string value) => new(value);

	public override string ToString() => Value;
}
```

`Create` validates, `Hydrate` trusts. That split is the subject of [Contracts and Data
Exchange](Contracts-And-Data-Exchange.md).

> **One trap, immediately.** The SDK strips a list of layer suffixes (`Core`, `Infrastructure`,
> `Persistence`, `Abstractions`, …) from `RootNamespace`, so a project called `Infrastructure` declares
> its types in `Acme`, not `Acme.Infrastructure`. If you want the suffix kept, remove it from the list
> **after** the import:
>
> ```xml
> <ItemGroup>
> 	<NamespaceRemoveSuffix Remove="Infrastructure" />
> </ItemGroup>
> ```

### Step 6 — Add a test project with an even shorter project file

`src/tests/Domain.UnitTests/Domain.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

The filename does the wiring. The SDK sees `UnitTests`, so it adds the TUnit runner, coverage, the
`[Category: Unit]` assembly attribute, and a `ProjectReference` to the project the name targets —
resolved by probing `../Domain/`, `../../Domain/`, `../src/Domain/` and `../../src/Domain/`, and skipped
silently when none of them exists. A test class then lives in the namespace of the type it tests:

```csharp
namespace Acme.Domain;

public sealed class CustomerIdTests
{
	[Test]
	public async Task Create_GivenAnEmptyValue_Throws()
	{
		// Arrange
		// Act
		Action act = () => CustomerId.Create("");

		// Assert
		await Assert.That(act).Throws<ArgumentException>();
	}
}
```

### Step 7 — Add the shared testing project

`src/tests/SharedTestingInfra/SharedTestingInfra.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<ItemGroup>
		<ProjectReference Include="../../src/Domain/Domain.csproj" />
	</ItemGroup>
</Project>
```

The name is one of a recognised set, so the SDK treats it as a shared testing project: it gets the test
packages but not the runner, stays a library, and is referenced automatically by the test projects
beside it. This is where `TestBuilder` lives — see [Testing and Test Data](Testing-And-Test-Data.md).

### Step 8 — Point the shared pipeline at the solution

`.config/purview-build.json`:

```json
{
	"$schema": "https://raw.githubusercontent.com/purview-dev/build/main/purview-build.schema.json",
	"Build": {
		"Solution": "src/Acme.slnx",
		"TestRoot": "src/tests",
		"TestPatterns": "*Tests.csproj",
		"TestFilter": "/*/*/*/*[Category=Unit]",
		"RunPack": false,
		"ValidatePack": false
	},
	"Release": {
		"Mode": "None"
	}
}
```

Paths are anchored to the **repository root**, not to the configuration file, so moving the file into
`.config/` does not change what it points at.

`RunPack` and `ValidatePack` are off because this repository ships no package. The shared workflow
defaults both to `true`, so leaving them unset would give a green local build and a failing pipeline.
See [Verification Model](Verification-Model.md).

`TestPatterns` is left at its default, `*Tests.csproj`, on purpose. Discovery and tiering are two
mechanisms, and only one of them should carry the tier: `TestPatterns` chooses which project files are
discovered, `TestFilter` chooses which discovered tests run. Leave discovery wide and the day you add
`Domain.IntegrationTests` it is kept out of the pull-request tier by its category alone, with no second
edit. Narrow `TestPatterns` and you have to remember both. See [Verification
Model](Verification-Model.md).

### Step 9 — Add the caller workflow

`.github/workflows/pr.yml`:

```yaml
name: PR

on:
  pull_request:
    branches: [main]
    types: [opened, synchronize, reopened, ready_for_review]

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: Build and test
    uses: purview-dev/build/.github/workflows/purview-build.yml@main
    with:
      run-pack: false
      validate-pack: false
      test-filter: "/*/*/*/*[Category=Unit]"
    secrets: inherit
```

The two pack switches repeat `Build:RunPack` and `Build:ValidatePack` from the configuration, so the
workflow and the JSON agree about what a green build means. The workflow is a caller; the pipeline
itself lives in one shared repository. See [Verification Model](Verification-Model.md) and
[Release Flow](Release-Flow.md).

### Step 10 — Add the wiki

`mkdocs.yml`:

```yaml
site_name: Acme
site_description: Developer documentation for Acme
repo_url: https://github.com/acme/acme
edit_uri: edit/main/docs/wiki/
docs_dir: docs/wiki

theme:
  name: material
plugins: [search, techdocs-core]
markdown_extensions: [admonition, attr_list, md_in_html, pymdownx.details, pymdownx.superfences]
```

Then `docs/wiki/index.md`, `docs/wiki/Home.md` and `docs/wiki/_Sidebar.md`, and the pages behind them.
`_Sidebar.md` is the navigation list, so a new page that is not in it is a page nobody finds. See
[Documentation Convention](Documentation-Convention.md).

### Step 11 — Make the repository legible to an agent

Add `AGENTS.md` at the root: purpose, layout table, conventions, completion checklist, local commands.
Then install the SDK's own skills with `npx skills add purview-dev/build-sdk`, so an agent gets that
guidance without you writing it. Installation is explicit - no build writes into an agent directory.
See [Agent Enablement](Agent-Enablement.md).

### Step 12 — Add the local tools and the commands that run them

Step 13 runs `just`, and `just lint-check` runs CSharpier. Neither exists yet, so create both now.

`.config/dotnet-tools.json` is the tool manifest, so CSharpier is restored at a pinned version rather than
installed globally per machine:

```json
{
	"version": 1,
	"isRoot": true,
	"tools": {
		"csharpier": {
			"version": "1.3.0",
			"commands": ["csharpier"],
			"rollForward": true
		},
		"purview.build": {
			"version": "0.5.3",
			"commands": ["purview-build"],
			"rollForward": false
		}
	}
}
```

`Purview.Build` belongs here too, for the same reason CSharpier does: the manifest pins the version, and
`dotnet tool restore` is the one command that installs it. A repository should never hand-roll
`dotnet tool install --tool-path` for it.

Run `dotnet tool restore` once after adding it.

`Justfile` at the repository root is the list of supported commands. The point of it is that every
command a contributor needs is discoverable from `just --list`, so start with the four the next step uses:

```text
set quiet

solution_file := "./src/Acme.slnx"
build_configuration := "Debug"
pipeline_tool := "dotnet purview-build"

# Builds the solution
build *args:
    dotnet build "{{ solution_file }}" --configuration "{{ build_configuration }}" {{ args }}

# Runs the tests
test filter="/*/*/*/*/" *args:
    dotnet test --solution "{{ solution_file }}" --configuration "{{ build_configuration }}" \
        --treenode-filter={{ filter }} {{ args }}

# Checks formatting with CSharpier
lint-check:
    dotnet csharpier check .

# Fixes formatting with CSharpier
lint-fix:
    dotnet csharpier format .

# Runs the PR pipeline (restore, build, lint, tests) through the shared tool
pipeline-pr *args:
    dotnet tool restore
    {{ pipeline_tool }} {{ args }}
```

There is deliberately no `Justfile` in `templates/`: the recipes are the list of things *your* repository
actually does, and a copied list of someone else's is noise. The five above are the floor, not the shape.

### Step 13 — Prove it

```text
just build          # dotnet build (Debug)
just test           # dotnet test
just lint-check     # CSharpier check
just pipeline-pr    # restore, build, lint, test through the shared Purview.Build tool
```

Then break a rule on purpose — add a `ProjectReference` from the domain to the layer above it, or rename
a project file so it no longer matches its folder — and confirm the build refuses. A convention you have
not watched fail is a convention you are guessing about.

### Step 14 — Or copy the templates

Most of the files in steps 1–12 have a copy-paste version in
[`templates/`](https://github.com/purview-dev/engineering-playbook/tree/main/templates), with `Acme` standing
in for your namespace prefix and a comment explaining the one thing about each file that is easy to get
wrong. The [templates README](https://github.com/purview-dev/engineering-playbook/blob/main/templates/README.md)
maps each template to its destination path and lists which files each profile needs.

Four things in the steps above are deliberately **not** templated, because a copy of them would be wrong
on arrival: the solution file (its paths depend on your project list), the two `.csproj` files (they are
empty by design), and the `Justfile` (see step 12). `templates/README.md` says which, and why.

## When the repository persists data

The steps above build a repository with no store, which is the floor. When persistence arrives, add the
three conditional rules on their own pages rather than inventing a shape:

| Concern | Rule | Page |
| -- | -- | -- |
| The context and the model | `DbContext` is the boundary; map value objects once; design time matches runtime; migrate deliberately | [EF Core Integration](EF-Core-Integration.md) |
| A value known before storage | Derive it with UUIDv5 over an installation-scoped namespace and key it `ValueGeneratedNever()` | [Well-Known Identifiers](Well-Known-Identifiers.md) |
| Every other GUID key | One strategy per store, applied by a model-finalising convention | [GUID Key Strategy](Guid-Key-Strategy.md) |

The working example is `samples/Acme.Persistence`, run with `just samples`. It sits outside `src/` so the
reference implementation in this repository stays in-memory and the canonical solution keeps its shape.

## How it is enforced

Step 13 proved it by breaking a rule on purpose. The table below is the same statement for the whole
repository: which concern lives where, and what refuses a change that breaks it.

| Concern | Where it lives | Enforced by |
| -- | -- | -- |
| Framework, language version, nullable, analyzers | `global.json` + the SDK import | The build |
| Package versions, and a family moving as one | `Directory.Packages.props` | The build (`NU1010`) for presence; review for the property style |
| Assembly and namespace identity | `NamespacePrefix` + the folder name | The build |
| Code style | Bootstrapped `.editorconfig` | The build, with the style policy validated |
| Test wiring | The `.csproj` filename | The build |
| Test data | `TestBuilder` in `SharedTestingInfra` | Review |
| Persistence identifiers and mapping | `Persistence` project, a model convention, `ValueGeneratedNever()` on derived keys | The architecture policy for direction; the sample and its checks for the conventions |
| Architecture | Dependency-direction tests + `.config/architecture-policy.json` | The build |
| Verification tiers | `TestFilter` in `purview-build.json` | The pipeline |
| Documentation | `docs/wiki` + `_Sidebar.md` | The build (`DocumentationTests`) for navigation and fences; review for the rest |

## At your scale

This page is the floor, not the whole playbook. [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
decides which parts to take for a small, medium or large repository, and [Worked
Examples](Worked-Examples.md) walks the reference implementation that results from taking all of them.

## See also

- [Engineering Principles](Engineering-Principles.md)
- [Repository Shape](Repository-Shape.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
- [Worked Examples](Worked-Examples.md)
