# Getting Started

This page builds the smallest repository that satisfies every rule in the playbook, in the order you
would actually create the files. Each step says what it buys you, so you can stop when you have taken
what you need — [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md) is the short version of
that decision.

The finished result is a repository whose defaults come from one MSBuild SDK, whose test projects are
wired by their filenames, whose architecture is asserted by a test, and whose documentation is a wiki
the build already knows how to find.

## Step 1 — Pin the SDK in `global.json`

```json
{
	"sdk": {
		"allowPrerelease": false
	},
	"msbuild-sdks": {
		"Purview.BuildSdk": "1.0.4"
	},
	"test": {
		"runner": "Microsoft.Testing.Platform"
	}
}
```

`msbuild-sdks` is what makes `<Import Sdk="Purview.BuildSdk" Project="Sdk.props" />` resolvable: MSBuild
restores the SDK as a NuGet package and reads its `.props`/`.targets`. `test.runner` selects the
Microsoft Testing Platform, which is the runner TUnit expects.

The SDK writes this file for you if it is missing, but pin the version explicitly rather than relying on
the bootstrap's fallback.

## Step 2 — Declare the versions the SDK injects, in `Directory.Packages.props`

Central Package Management is on by default, so every `PackageReference` the SDK adds has to have a
matching `PackageVersion` or restore fails with `NU1010`:

```xml
<Project>
	<PropertyGroup>
		<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
	</PropertyGroup>
	<ItemGroup>
		<PackageVersion Include="Purview.Telemetry.SourceGenerator" Version="5.0.1" />
		<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="10.10.0" />
		<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.401" />
		<PackageVersion Include="TUnit" Version="1.72.4" />
		<PackageVersion Include="TUnit.Core" Version="1.72.4" />
		<PackageVersion Include="TUnit.Mocks" Version="1.72.4" />
		<PackageVersion Include="Bogus" Version="35.6.5" />
	</ItemGroup>
</Project>
```

The first three are injected by the SDK itself — telemetry and SourceLink are on by default. The last
four are the test stack: TUnit is the framework, TUnit.Mocks the substitute library, Bogus the test-data
generator. See [Testing and Test Data](Testing-And-Test-Data.md).

## Step 3 — Import the SDK, and name the namespace prefix

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

## Step 4 — Add the solution

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

## Step 5 — Create a project with a two-line project file

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

## Step 6 — Add a test project with an even shorter project file

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

## Step 7 — Add the shared testing project

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

## Step 8 — Point the shared pipeline at the solution

`.config/purview-build.json`:

```json
{
	"$schema": "https://raw.githubusercontent.com/purview-dev/build/main/purview-build.schema.json",
	"Build": {
		"Solution": "src/Acme.slnx",
		"TestRoot": "src/tests",
		"TestPatterns": "*UnitTests.csproj",
		"TestFilter": "/*/*/*/*[Category=Unit]"
	},
	"Release": {
		"Mode": "None"
	}
}
```

Paths are anchored to the **repository root**, not to the configuration file, so moving the file into
`.config/` does not change what it points at.

## Step 9 — Add the caller workflow

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
      test-filter: "/*/*/*/*[Category=Unit]"
    secrets: inherit
```

The workflow is a caller; the pipeline itself lives in one shared repository. See [Verification
Model](Verification-Model.md) and [Release Flow](Release-Flow.md).

## Step 10 — Add the wiki

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

## Step 11 — Make the repository legible to an agent

Add `AGENTS.md` at the root: purpose, layout table, conventions, completion checklist, local commands.
The SDK also mirrors a set of SDK skills into `.agents/` on build, so an agent gets the SDK's own
guidance without you writing it. See [Agent Enablement](Agent-Enablement.md).

## Step 12 — Prove it

```text
just build          # dotnet build (Debug)
just test           # dotnet test
just lint-check     # CSharpier check
just pipeline-pr    # restore, build, lint, test through the shared Purview.Build tool
```

Then break a rule on purpose — add a `ProjectReference` from the domain to the layer above it, or rename
a file so it no longer matches its folder — and confirm the build refuses. A convention you have not
watched fail is a convention you are guessing about.

## What you have now

| Concern | Where it lives | Enforced by |
| -- | -- | -- |
| Framework, language version, nullable, analyzers | `global.json` + the SDK import | The build |
| Assembly and namespace identity | `NamespacePrefix` + the folder name | The build |
| Code style | Bootstrapped `.editorconfig` | The build, with the style policy validated |
| Test wiring | The `.csproj` filename | The build |
| Test data | `TestBuilder` in `SharedTestingInfra` | Review |
| Architecture | Dependency-direction tests | The build |
| Verification tiers | `TestFilter` in `purview-build.json` | The pipeline |
| Documentation | `docs/wiki` + `_Sidebar.md` | Review |

## See also

- [Engineering Principles](Engineering-Principles.md)
- [Repository Shape](Repository-Shape.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
- [Worked Examples](Worked-Examples.md)
