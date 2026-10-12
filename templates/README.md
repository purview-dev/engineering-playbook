# Templates

Ready-to-copy starter files for a repository adopting the playbook. Each file here is the version that
already exists in this repository, with `Acme` standing in for your own namespace prefix.

| Template | Copy to | Profile |
| -- | -- | -- |
| [`global.json`](global.json) | `global.json` | All |
| [`Directory.Packages.props`](Directory.Packages.props) | `Directory.Packages.props` | All |
| [`Directory.Build.props`](Directory.Build.props) | `src/Directory.Build.props` | All |
| [`Directory.Build.targets`](Directory.Build.targets) | `src/Directory.Build.targets` | All |
| [`purview-build.json`](purview-build.json) | `.config/purview-build.json` | All |
| [`dotnet-tools.json`](dotnet-tools.json) | `.config/dotnet-tools.json` | All |
| [`pr.yml`](pr.yml) | `.github/workflows/pr.yml` | All |
| [`release.yml`](release.yml) | `.github/workflows/release.yml` | All |
| [`AGENTS.md`](AGENTS.md) | `AGENTS.md` | All |
| [`mkdocs.yml`](mkdocs.yml) | `mkdocs.yml` | All |
| [`TestBuilder.cs`](TestBuilder.cs) | `src/tests/SharedTestingInfra/Builders/TestBuilder.cs` | Medium and up |

## How to use them

1. **Pick a profile.** [Profiles](#profiles) below says which files each one needs.
2. **Replace `Acme`.** It appears in `Directory.Build.props` (as `NamespacePrefix`), in the names in
   `AGENTS.md`, and in the paths in `purview-build.json`.
3. **Pin the versions.** The SDK version in `global.json` and the package versions in
   `Directory.Packages.props` are the ones this repository builds with today. Check them against the SDK's
   current release before you commit.
4. **Read the comment in each file.** Every template explains the one thing about it that is easy to get
   wrong — the order of the `NamespacePrefix` and the import, the `NU1010` trap, why the workflow repeats the
   test filter that is already in the JSON.
5. **Delete what you do not need.** A template you keep but do not understand is worse than a file you wrote.

The files are deliberately **not** a scaffolding tool. There is no generator, no placeholder syntax and no
template engine, because a file you copy and edit is a file you have read, and the whole point of the
playbook is that the conventions are understood rather than applied.

## Profiles

### Small — one project

```text
global.json
Directory.Packages.props
src/Directory.Build.props
src/Directory.Build.targets
purview-build.json
dotnet-tools.json
pr.yml
AGENTS.md
mkdocs.yml
```

Then create `src/src/<Project>/<Project>.csproj` and `src/tests/<Project>.UnitTests/<Project>.UnitTests.csproj`
as two empty `<Project Sdk="Microsoft.NET.Sdk"></Project>` files, and add both to `src/Acme.slnx`.

Skip `TestBuilder.cs` until you have a domain type worth building; add it with the first test that needs one,
which is usually the first test.

### Medium — a few layers

Everything in Small, plus:

- `TestBuilder.cs` in `src/tests/SharedTestingInfra/Builders/`.
- One test project per layer, named `*.UnitTests`.
- `src/tests/Architecture.UnitTests/` with the rules from
  [Architecture as Tests](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Architecture-As-Tests.md).
- A second CI job for any suite that needs a real resource.

### Large — several packages

Everything in Medium, plus:

- `Sdk/README.md` and `Sdk/buildTransitive/**` per packable project.
- `PackValidation` in `purview-build.json`, with `RequiredContent` as an exhaustive manifest.
- `run-pack: true` and `validate-pack: true` in `pr.yml`.
- A consumer-verification script and a `just verify-consumers` recipe.
- `release-mode: NuGet` in `release.yml`.

## What is not here

- **`.editorconfig`, `.gitignore`, `.gitattributes`.** The SDK bootstraps `.editorconfig` to the repository
  root on the first build. Take the other two from the SDK's `templates/` folder so they stay in step with
  it.
- **A solution file.** Its shape depends on your project list, and a template with the wrong paths in it is a
  template that fails on the first build. The shape is in
  [Getting Started](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Getting-Started.md).
- **`Justfile`.** It is worth writing rather than copying: the recipes are the list of things you actually do,
  and a copied list of things somebody else does is noise.

## See also

- [Getting Started](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Getting-Started.md)
- [Adopting This at Your Scale](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Adopting-This-At-Your-Scale.md)
- [Repository Shape](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Repository-Shape.md)
