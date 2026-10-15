# Agent Skills Distribution

## The rule

A skill is published **once**, at the repository root in `skills/<name>/SKILL.md`, and travels to its
consumers by two routes that share that single copy:

| Route | Who it serves | Mechanism |
| -- | -- | -- |
| NuGet package → `.agents/skills/` | A .NET repository that references the package | Mirrored on build by `Purview.BuildSdk` |
| Git repository → agent directories | Any agent, any language, via `npx skills` | Installed on request by the skills CLI |

**Installation into an agent's own directories is always explicit.** It is never a side effect of
`dotnet restore`, `dotnet build`, a design-time build in an IDE, or a test run.

Version compatibility is resolved from metadata the package already carries — the skills it ships and
the commit it was built from — so there is no hand-maintained package-to-skill manifest.

## Why

A skill is guidance that goes stale against the code it describes. Three failures follow from that,
and the design answers each one.

**Guidance that does not travel with the code.** If a library's conventions live only in its own
repository's wiki, every consumer re-derives them, and an agent in a consuming repository has no
access to them at all. Shipping the skill inside the package means a `PackageReference` is enough.

**Guidance that does not match the version in use.** A project pinned to an older release that
receives the newest skill is told about APIs it does not have. The nuspec records the commit the
package was built from, so the skill resolved is the skill *that version* shipped.

**An agent directory modified by a build.** A build that writes into `.claude/`, `.codex/` or a
peer's equivalent is a build with an invisible side effect: it mutates a developer's tooling, races
between parallel projects, and makes a restore non-reproducible. Resolution is cheap and automatic;
installation is a decision a person makes.

There is a fourth, subtler reason for the root `skills/` folder. The mirrored `.agents/` folder is
gitignored in every consuming repository — that is what stops a package upgrade appearing as a local
modification. A gitignored folder is absent from a git clone, so it cannot also be the source a
cloning installer reads. The authored copy has to live somewhere tracked.

## Skill format

The Agent Skills convention, unchanged:

```text
skills/
└── purview-example/
    ├── SKILL.md         required; YAML frontmatter plus the main workflow
    ├── references/      deeper material the skill links to, read on demand
    ├── scripts/         deterministic operations
    └── assets/          templates and fixtures
```

`SKILL.md` opens with frontmatter whose `description` is the activation trigger:

```markdown
---
name: purview-persistence
description: >-
  Use when a .NET project actually persists data and you are choosing a storage approach...
---
```

Use progressive disclosure. The frontmatter says *when* the skill applies; `SKILL.md` carries the
workflow; `references/` holds the detail that only some tasks need. A skill that inlines everything
costs context on every activation, whether or not the detail was relevant.

Link to the authoritative documentation rather than restating it. A skill that paraphrases a library's
own wiki is a second source of truth, and it is the one that will drift.

## Publication and distribution

**Diagram J — skill publication and distribution.**

```text
flowchart TD
    A["skills/&lt;name&gt;/SKILL.md<br/>the single authored copy"]

    A --> G["git: tracked at the repository root"]
    A --> P["IncludePurviewSkillsInPackage"]

    P --> N[".nupkg<br/>.agents/skills/&lt;name&gt;/<br/>+ generated .gitignore<br/>+ nuspec repository url and commit"]
    N --> F["NuGet feed"]

    G --> C["skills CLI<br/>clone at ref"]
    F --> M["Purview.BuildSdk mirror<br/>reads the restore graph"]

    C --> AD["Agent directories<br/>.claude/, .codex/, ..."]
    M --> DA[".agents/skills/<br/>gitignored mirror"]
```

The nuspec's `<repository>` element is written by SourceLink for every package built by the SDK. It
is what lets a consumer get from a restored package back to the exact source commit.

## Package-to-skill discovery

No custom metadata file is introduced, because the package already carries both halves of the mapping.

**Diagram K — package-to-skill discovery and Build SDK resolution.**

```text
flowchart TD
    A["project.assets.json<br/>every restored package: id, version, path"] --> B{"package has<br/>.agents/skills/ ?"}
    B -- no --> X["Not a skill source"]
    B -- yes --> C["Skill names = folders containing a SKILL.md"]
    C --> D["Read the package nuspec"]
    D --> E{"repository url is GitHub<br/>and commit present?"}
    E -- yes --> F["PurviewSkillSource<br/>owner/repo#commit + skill names"]
    E -- no --> G["PurviewUnresolvableSkillSource<br/>PRSKL0002, nothing installed"]
```

A folder under `.agents/skills/` counts as a skill only when it contains a `SKILL.md`; a `references/`
or `scripts/` folder beside one is part of that skill, not another.

## Responsibilities

The separation matters more than any single piece of it.

**Diagram K (detail) — installer and Build SDK responsibilities.**

```text
flowchart LR
    subgraph Package["Skill metadata - the package itself"]
        M1[".agents/skills/*"]
        M2["nuspec repository element"]
    end

    subgraph SDK["Purview.BuildSdk"]
        R["Skill resolver<br/>ResolvePurviewSkillSources<br/>decides nothing about agents"]
        T["Explicit targets<br/>ListPurviewSkills<br/>InstallPurviewSkills"]
        D["CLI detection<br/>DetectPurviewSkillsCli"]
    end

    subgraph CLI["Skill installer - npx skills"]
        I1["Agent-specific destinations"]
        I2["symlink or copy"]
        I3["skills-lock.json"]
    end

    M1 --> R
    M2 --> R
    R --> T
    D --> T
    T -->|"invokes, never installs itself"| CLI
```

The SDK does not maintain a copy of each skill per supported agent. Agent discovery paths differ, are
numerous, and change on their own schedule; the CLI tracks them, so the SDK does not have to.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| A package's skills are discoverable from the restore graph | `ResolvePurviewSkillSources`, tested in `PurviewSkillResolutionTests` |
| The resolved skill matches the restored package version | The nuspec commit is the ref; tested |
| A package that cannot be pinned is reported, not guessed | `PRSKL0002` warning; tested |
| Resolution without a restore gives an actionable error | `PRSKL0001` error |
| An ordinary restore-and-build installs nothing | Test asserts no lockfile and no agent directory after a full build |
| Repeated installation is idempotent | The skills CLI skips a skill already installed from the same source and ref |
| Root `skills/` reaches the package's `.agents/skills/` | `AgentPackFolderTests`, asserted against the built `.nupkg` |
| A packaged skill does not show as a local modification | Generated per-skill `.gitignore`, asserted in the same test |
| A skill's frontmatter is valid, its name matches its folder and is unique | `ValidatePurviewSkills`, which `IncludePurviewSkillsInPackage` depends on — so a malformed skill fails the pack (`PRSKL0005`) |
| Every `references/...` link in a skill resolves | The same target |
| The guidance in a skill is good, and an agent follows it | **Review** |

The last row is a review obligation and stays one. A build can check that a skill is well formed; it
cannot check that prose is correct or that an agent obeyed it. See
[Agent Guidance Skills](Agent-Guidance-Skills.md) for the scenarios used to review that by hand.

## Version resolution

**Diagram K (detail) — version resolution and compatibility diagnostics.**

```text
flowchart TD
    A["Referenced package version<br/>from project.assets.json"] --> B{"nuspec repository<br/>commit present?"}
    B -- no --> W["PRSKL0002 warning<br/>nothing installed<br/>the package's own mirror still delivers it"]
    B -- yes --> C["owner/repo#commit<br/>pinned and reproducible"]
    C --> D["skills CLI records source + ref<br/>in skills-lock.json"]
    D --> E["npx skills experimental_install<br/>restores the same set"]
```

Pinning to a commit rather than a tag is deliberate: a tag can be moved, and a package can be built
from a commit that was never tagged — a pre-release, for instance. The commit is the only identifier
guaranteed to correspond to the restored package.

Where exact compatibility cannot be determined, the SDK warns and installs nothing. Installing the
default branch's skill against an older package is the failure this is designed to avoid.

## Using it

```bash
dotnet restore                                        # resolution reads the restore graph
dotnet msbuild <project> -t:ListPurviewSkills         # diagnostics; installs nothing
dotnet msbuild <project> -t:InstallPurviewSkills      # explicit install, via npx skills
```

| Property | Default | Purpose |
| -- | -- | -- |
| `PurviewSkillsSourceFolder` | `<repo root>/skills` | Where authored skills live |
| `PurviewPackRepositorySkills` | `false` | Opts a project into packing them |
| `PurviewSkillsToPack` | *(empty — all)* | The subset of skills *this* project ships |
| `PurviewSkillsAgents` | `*` | Which agents to install to |
| `PurviewSkillsCommand` | *(detected)* | Overrides the CLI launcher |
| `PurviewSkillsSource` | *(empty)* | Install from an explicit git source instead of from packages |

### Detecting the installer

The installer is Node tooling, and a machine that can build .NET need not have it. The SDK probes
`PATH` for `skills` first (a global or local install — no download, works offline), then `npx`,
honouring `PATHEXT` on Windows where both are `.cmd` shims. `PATH` is read directly rather than by
spawning a version check: deterministic, no process, and it cannot hang on a cold `npx` download.

When `npx` is the fallback it is pinned to a major (`skills@1`), so a future breaking release of the
CLI cannot silently change what the target does.

Neither found is an actionable error naming the fix, not a raw `'npx' is not recognized`. And
`ListPurviewSkills` only *reports* availability — resolving and reporting needs no Node tooling, so
the diagnostic path always works.

You can also install from a named repository, bypassing package resolution entirely:

```bash
dotnet msbuild <project> -t:InstallPurviewSkills -p:PurviewSkillsSource=purview-dev/engineering-playbook
```

That is the escape hatch for a repository that wants a Purview skill it has no `PackageReference`
for. The caller owns the ref — append `#<ref>` to pin it — and no restore is required.

### One repository, several packages

A repository usually produces more than one package, and a skill belongs to the one whose library it
describes — `Purview.Results.AspNetCore`'s HTTP-mapping skill has no business in `Purview.Results`.
`PurviewSkillsToPack` names that subset per project:

```text
skills/                                     ← one authored copy of each
├── purview-results-core                  ─┐
├── purview-results-union-errors          ─┴─► Purview.Results
├── purview-results-http-mapping          ───► Purview.Results.AspNetCore
├── purview-results-zodsharp-validation   ───► Purview.Results.ZodSharp
└── purview-results-zodsharp-problems     ───► Purview.Results.ZodSharp.AspNetCore
```

A skill left out of every `PurviewSkillsToPack` is still installable from the git source and ships in no
package — which is how a repository keeps skills about *working in that repository* (test runners, commit
conventions) separate from the guidance it publishes to consumers.

A name in `PurviewSkillsToPack` with no matching folder is `PRSKL0003`, so a renamed or deleted skill
fails the pack instead of silently vanishing from the package.

Updating and removing are the CLI's, not the SDK's: `npx skills update`, `npx skills remove`,
`npx skills list`.

### Troubleshooting

| Symptom | Cause |
| -- | -- |
| `PRSKL0001` | No `project.assets.json`. Run `dotnet restore`; resolution never restores for you. |
| `PRSKL0002` | The package's nuspec has no GitHub repository URL or no commit — usually SourceLink disabled at pack time. |
| `PRSKL0003` | `PurviewSkillsToPack` names a skill folder that does not exist — usually a rename. |
| `PRSKL0005` | A skill is malformed: no `SKILL.md`, no frontmatter, no `name`/`description`, a name that does not match its folder, or a broken `references/` link. |
| `PRSKL0004` | Neither `skills` nor `npx` is on `PATH`. Install Node.js, or `npm install -g skills`. `ListPurviewSkills` still works. |
| "No skills found" from the CLI | The source repository has no root `skills/` folder. The CLI does not discover skills in nested or dotted folders, even with `--full-depth`. |
| Skills appear in `.agents/` but no agent sees them | `.agents/` is the mirror, not an agent directory. Run the install target. |
| `npx` not available | The install target needs Node (`PRSKL0004` names the fix). Resolution and `ListPurviewSkills` do not. |

## Publishing skills for a new library

1. Author the skill at the repository root: `skills/<name>/SKILL.md`.
2. Set `PurviewPackRepositorySkills=true` on the project that ships it, and name it in that project's
   `PurviewSkillsToPack` when the repository produces more than one package.
3. Leave SourceLink enabled, so the nuspec records the repository and commit.
4. Verify with `npx skills add <owner>/<repo> -l` — if it reports no skills, the folder is in the
   wrong place.
5. Verify the package with `dotnet pack` and inspect `.agents/skills/` in the `.nupkg` — that is what
   existing consumers receive, and it should not change for skills you did not touch.

## At your scale

- **One repository, one agent.** You do not need any of this. Put `SKILL.md` files in `skills/` and
  run `npx skills add .` when they change. The format is the part worth adopting first.
- **Several repositories sharing conventions.** Publish skills from the repository that owns the
  convention and install them with the CLI. Still no SDK required.
- **A library with consumers.** Ship skills in the package. The guidance arrives with the dependency,
  and it arrives at the version the consumer is actually on.
- **A platform with many packages and agents.** The full split above: metadata in the package,
  resolution in the build, installation in a tool that owns agent paths. The thing to copy is the
  separation, not the implementation.

## See also

- [Agent Enablement](Agent-Enablement.md) — `AGENTS.md`, `.agents/`, and what makes a good skill
- [Documentation Convention](Documentation-Convention.md)
- [skills CLI](https://github.com/vercel-labs/skills)
