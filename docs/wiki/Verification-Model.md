# Verification Model

## The rule

Checks are tiered by cost, and a repository declares which tiers its pipeline runs. A pull request runs the
fast, deterministic tier. Anything that needs a real external resource is selected deliberately — locally,
or on a runner that has the resource.

The tier a test belongs to is a property of **where it lives**, not of an attribute someone remembered to
add: the SDK reads the test project's name suffix and emits an assembly-level category for it.

| Project name | Category | Runs on a pull request? |
| -- | -- | -- |
| `<Target>.UnitTests` | `Unit` | Yes |
| `Architecture.UnitTests` | `Unit` | Yes |
| `<Target>.ContractTests` | `Contract` | Usually |
| `<Target>.AcceptanceTests` | `Acceptance` | No — its own job |
| `<Target>.IntegrationTests` | `Integration` | Only when the resource is available |
| `<Target>.E2ETests` | `E2E` | No |

The category is a pattern match rather than a lookup — the SDK takes the word before `Tests` — so
`Acceptance` needs nothing added to the pipeline to be recognised. `Acceptance` is also in the SDK's
`TestType` list, which is what generates the `InternalsVisibleTo` back to the target project.

`Architecture.UnitTests` is named that way **deliberately**, and the obvious alternative is a trap. The
SDK takes the category from the word before `Tests`, so `Architecture.ArchitectureTests` would get
`Category=Architecture` — and a pull-request filter of `[Category=Unit]` would quietly stop running the
dependency-direction rules. The rules are fast, deterministic and have no external resource, so they
belong in the `Unit` tier; naming them `Architecture.UnitTests` is what puts them there.

## Why

A gate is only useful if it is credible, and credibility comes from speed. The moment the pull-request
build needs a database, a container runtime and four minutes of waiting, somebody makes it optional — and
then nothing is gated.

The tiering also changes what a test is allowed to assume. A `Unit` test may assume it runs everywhere, so
it may not touch the network, the clock or the file system. An `Integration` test may assume it runs
somewhere real, and pays for that by having to be selected.

## How a tier is expressed

Three steps, none of which is a hand-written attribute:

1. **The project name.** `Domain.UnitTests.csproj` — the SDK detects `Unit` and emits
   `[assembly: Category("Unit")]`.
2. **The filter.** The pipeline passes `--treenode-filter`, and the repository declares it once:

   ```json
   {
   	"Build": {
   		"Solution": "src/Acme.slnx",
   		"TestRoot": "src/tests",
   		"TestPatterns": "*Tests.csproj",
   		"TestFilter": "/*/*/*/*[Category=Unit]"
   	}
   }
   ```

3. **The run.** `dotnet test --solution ... --treenode-filter "/*/*/*/*[Category=Unit]"`.

The filter string is a path, not a search: `/*/*/*/*` matches the tree-node depth of a test, and
`[Category=Unit]` restricts it to the categorised assemblies. Because the category comes from the assembly,
adding a `Domain.IntegrationTests` project is enough to keep it out of the pull-request tier — no filter
edit, no attribute, no list to forget.

That last sentence is only true if **`TestPatterns` is left at its default**, `*Tests.csproj`.
`TestPatterns` and `TestFilter` are two different mechanisms, applied in that order: `TestPatterns`
decides which project files are *discovered* under `TestRoot`, and `TestFilter` decides which of the
discovered tests *run*. Narrow `TestPatterns` to `*UnitTests.csproj` and you have added a second place
that has to be edited before a new tier is ever seen — the list to forget, reintroduced. Keep the
default, express the tier in `TestFilter` alone, and reach for `TestPatterns` only when you genuinely
want a project excluded from discovery altogether. See [build: Configuration
Reference](https://github.com/purview-dev/build/blob/main/docs/wiki/Configuration-Reference.md).

## What the pipeline actually runs

The repository owns a caller workflow; the pipeline lives once, in
[build](https://github.com/purview-dev/build). The shared workflow's inputs are the contract:

| Input | Default | Effect |
| -- | -- | -- |
| `dotnet-version` | `10.0.x` | SDK used by the runner. Pin it when `global.json` pins one too. |
| `run-tests` | `true` | Enable discovered tests. |
| `run-lint` | `true` | Run CSharpier. |
| `run-pack` | `true` | Pack. |
| `validate-pack` | `true` | Validate the produced packages. |
| `test-filter` | *(from config)* | Tree-node filter. |
| `test-projects` | *(from config)* | Restrict which discovered projects run. |
| `release-mode` | `None` | `None`, `NuGet` or `GitHubRelease`. |

Two details are easy to get wrong:

- **The shared workflow's defaults are not your defaults.** It defaults to `run-pack: true` and
  `validate-pack: true`, so a repository that ships no package must turn both off — in the workflow *and*
  in `purview-build.json`, or the two disagree about what a green build means.
- **An optional input left empty is not the same as omitted.** Setting `Build__TestFilter` to an empty
  string overrides the repository's configuration, because environment variables outrank the JSON. The
  shared workflow therefore forwards `test-filter` and `test-projects` only when the caller supplied them.

## Slow tiers get their own job

The pattern for a suite that cannot run in the fast tier is a **second job in the same workflow**, not a
disabled test. [containers](https://github.com/purview-dev/containers) is the reference: the shared pipeline
runs the `[Category=Unit]` tier, and a separate `integration-docker` job on the same `ubuntu-latest` runner
runs the Docker-backed suites. The suites skip themselves when no daemon is reachable, so a non-zero result
is a real failure rather than a missing runtime.

[build-sdk](https://github.com/purview-dev/build-sdk) shows the other shape: a matrix over three test
projects, including an integration suite, because the whole point of that repository is that the SDK works
end to end.

Either way the rule holds: **the fast tier is always on, and the slow tier is explicit.**

A BDTest acceptance suite is a slow tier of this kind, with one addition: the job that runs it publishes the
`BDTest.Reporting` HTML report as a build artifact, because the report is the reason the suite is written
in that shape at all. A container-backed suite adds a different one: pin the backend with
`PURVIEW_CONTAINERS_BACKEND` in the job, so a runner without that runtime fails the job rather than
skipping every test and reporting green. Both are covered in [Testing and Test
Data](Testing-And-Test-Data.md).

## The configuration file

`purview-build.json` is probed for in a documented order, first match wins, and files are never merged:

```text
purview-build.json  →  .config/purview-build.json  →  .build/purview-build.json
→  build/purview-build.json  →  .purview/purview-build.json  →  .github/purview-build.json
```

Paths inside it — `Build:Solution`, `Build:TestRoot`, `PackValidation` globs — are anchored to the
**repository root**, wherever the file was found. Moving the file into `.config/` therefore does not change
what it points at, which is the whole reason it can be moved safely.

The file is validated against a JSON Schema generated from the tool's own settings, so a mistyped key is an
error naming the key rather than a line that is silently ignored. Add the `$schema` line and your editor
completes it.

## What this repository does

This playbook ships no NuGet package, so its pipeline turns packing off and its release is a tag:

```yaml
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

Three things are visible in that snippet, and all three are deliberate: the repository owns only the
caller; the pack switches match `.config/purview-build.json`; and the filter is the same string as the one
in the configuration, so the two cannot drift apart silently.

The repository also owns a runnable sample, `samples/Acme.Persistence`, that the pipeline does **not**
build. It sits outside `src/`, has no analyzers and is not in the canonical solution; `just samples` is
its verification, and it fails with a non-zero exit code when its identifier or model checks are untrue.
That is a deliberate fourth tier — a sample, not a test — and the page says so rather than counting it in
the fast job. [EF Core Integration](EF-Core-Integration.md) lists the checks the sample and an adopting
repository's integration suite should cover.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| The fast tier runs on every pull request | The caller workflow, and branch protection that makes it required |
| The tier is derived from the project name | The build (SDK category emission) |
| The repository's configuration is valid | The pipeline, which validates the JSON against the schema |
| The slow tier is run before a release | Review — and, where it matters, a release gate |
| The runnable sample still passes its own checks | **Review** — `just samples`, deliberately outside the pipeline |

## At your scale

- **One tier.** Everything is `UnitTests`. Do not add a filter yet.
- **A suite that needs a resource.** Add `<Project>.IntegrationTests`, keep it out of the filter, and give
  it a second job. Make the suite skip itself when the resource is absent, so a missing runtime is a skip
  and never a pass.
- **A package.** Turn on `run-pack` and `validate-pack`, and treat pack validation as the gate that stops a
  package shipping without the assets it documents.
- **A convention whose evidence is a sample.** Keep the sample outside the solution and give it a single
  command that fails loudly, as `just samples` does. Do not call it a test unless the pipeline runs it.
- **Several repositories.** Keep the pipeline shared and let each repository own only its caller workflow
  and its configuration. That is the arrangement that makes a fix in one place a fix everywhere.

## See also

- [Release Flow](Release-Flow.md)
- [Testing and Test Data](Testing-And-Test-Data.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [build: Configuration
  Reference](https://github.com/purview-dev/build/blob/main/docs/wiki/Configuration-Reference.md)
