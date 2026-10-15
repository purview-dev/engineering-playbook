# Agent Skills Distribution

## The rule

A skill is published **once**, at the repository root in `skills/<name>/SKILL.md`, and travels to its
consumers by **one** route: the skills CLI clones the git repository and installs into each agent's
own directories.

**Installation is always explicit.** It is never a side effect of `dotnet restore`, `dotnet build`, a
design-time build in an IDE, or a test run.

Nothing packs a skill into a NuGet package. `Purview.BuildSdk` 1.0.6 removed both the packing and the
build-time `.agents/` mirror that delivered it — see [Why one route](#why-one-route).

## Why

A skill is guidance that goes stale against the code it describes. Three failures follow, and the
design answers each one.

**Guidance that does not travel with the code.** If a library's conventions live only in its own
repository's wiki, every consumer re-derives them, and an agent in a consuming repository has no
access to them at all. Publishing the skill from the library's repository means one `npx skills add`
is enough.

**Guidance that does not match the version in use.** A project pinned to an older release that
receives the newest skill is told about APIs it does not have. The CLI installs from a **ref**, and
records it, so a repository can pin a skill to the commit or tag that matches the version it uses.

**An agent directory modified by a build.** A build that writes into `.claude/`, `.codex/` or a
peer's equivalent has an invisible side effect: it mutates a developer's tooling, races between
parallel projects, and makes a restore non-reproducible. Installation is a decision a person makes.

## Why one route

There used to be two: the CLI, and a NuGet package whose `.agents/skills/` content the SDK mirrored
into the consuming repository on build.

The second was removed because it was **a second writer on the first one's paths**. The CLI's
universal install location *is* `.agents/skills/`. So a build could silently overwrite a pinned
install, and the SDK's own drift check would then compare a lockfile ref against content the mirror
had already replaced. Duplication was not neutral here; it was a correctness problem.

It also never closed the gap it appeared to close. `.agents/` is a convention, not a directory any
agent reads natively — Claude Code, Codex, Copilot and the rest each look somewhere else. Mirroring a
skill into `.agents/` made it *present*, not *active*. The CLI knows where each agent actually looks,
and that knowledge changes on its own schedule, so it belongs in a tool that tracks it rather than in
a build.

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

The specification defines six fields and no more: `name`, `description`, `license`, `compatibility`,
`metadata` and `allowed-tools`. Anything else belongs under `metadata`. `name` is 1–64 characters of
lowercase `a-z0-9` and single hyphens, and **must match the folder name** — the CLI installs by the
frontmatter name while a human reads the folder, so a mismatch installs under a name nobody expects.

Use progressive disclosure. The frontmatter says *when* the skill applies; `SKILL.md` carries the
workflow; `references/` holds the detail only some tasks need. A skill that inlines everything costs
context on every activation, whether or not the detail was relevant — keep `SKILL.md` under 500
lines.

Link to the authoritative documentation rather than restating it. A skill that paraphrases a
library's own wiki is a second source of truth, and it is the one that will drift.

**Never link from one skill into another skill's folder.** Only a skill's own folder is installed
with it, so a relative path that escapes it works in the authoring repository and nowhere else. Name
the other skill in prose.

## Naming

Skills install into **one flat directory per agent**, shared with every skill a developer has
installed from anywhere. `sdk-configuration`, `project-placement`, `dotnet-tunit` and
`git-conventional-commits` are all names somebody else will also pick, and the loser of a collision
is whichever installed second.

So every published skill carries a vendor-and-product prefix: `purview-value-objects-efcore`,
`purview-sourcegenerator-codewriter`, `purview-telemetry-diagnostics`. The prefix is not decoration;
it is the only thing keeping two unrelated skills from being the same file.

## Publication and distribution

**Diagram J — skill publication and distribution.**

```text
flowchart TD
    A["skills/&lt;name&gt;/SKILL.md<br/>the single authored copy"]
    A --> G["git: tracked at the repository root"]
    G --> C["skills CLI<br/>clone at ref"]
    C --> AD["Agent directories<br/>.claude/, .codex/, ..."]
    C --> L["skills-lock.json<br/>source + ref, committed"]
```

The CLI discovers skills **only** in a root `skills/` folder. It does not look in nested or dotted
folders, even with `--full-depth`.

`skills-lock.json` is the committed record of what a repository installed, so the same set can be
restored later with `npx skills experimental_install`. Everything the CLI writes under
`.agents/skills/` and the per-agent directories is gitignored — it is all reproducible from the
lockfile.

## Responsibilities

The separation matters more than any single piece of it.

```text
flowchart LR
    subgraph Repo["The authoring repository"]
        S["skills/&lt;name&gt;/SKILL.md"]
        V["ValidatePurviewSkills (pack-time)<br/>PurviewSkillTests (test-time)"]
    end

    subgraph SDK["Purview.BuildSdk"]
        T["Explicit targets<br/>ListPurviewSkills<br/>InstallPurviewSkills"]
        D["CLI detection<br/>DetectPurviewSkillsCli"]
        R["Repository-root resolution<br/>FindPurviewRepositoryRoot"]
    end

    subgraph CLI["Skill installer - npx skills"]
        I1["Agent-specific destinations"]
        I2["symlink or copy"]
        I3["skills-lock.json"]
    end

    S --> V
    R --> T
    D --> T
    T -->|"invokes, never installs itself"| CLI
```

The SDK keeps no copy of a skill per supported agent. Agent discovery paths differ, are numerous, and
change on their own schedule; the CLI tracks them, so the SDK does not have to.

Two invariants govern how the SDK launches it:

- **Any package runner, defaulting to `npx`.** `PurviewSkillsRunner` selects one (`npx`, `bunx`,
  `bun x`, `pnpm dlx`, `yarn dlx`, `deno run -A npm:`); an unknown runner falls through to
  `<runner> skills`. Detection probes `PATH` for `skills` first, then the configured runner, then the
  candidates — honouring `PATHEXT` on Windows. `PATH` is read directly rather than by spawning a
  version check: deterministic, no process, and it cannot hang on a cold download.
- **Always the repository root.** `skills-lock.json` and the agent directories belong to the
  repository, not to a project, so every skill target runs from one resolved root. There is
  deliberately no project-directory fallback — that would scatter a lockfile and an agent directory
  through the tree, one per project.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| A skill's frontmatter is valid, its name matches its folder and is unique | `ValidatePurviewSkills` before `Pack` (`PRSKL0005`) |
| The same rules hold on every test run | `PurviewSkillTests` from `Purview.Skills.Testing`, inherited by one derived class per repository |
| Every `references/...` link in a skill resolves, and resolves *inside* the skill | Both of the above |
| `SKILL.md` stays within the recommended size | `PurviewSkillTests` |
| An empty `skills/` folder is reported rather than passing vacuously | `PurviewSkillTests` discovery rule |
| An ordinary restore-and-build installs nothing | Test asserts no lockfile and no agent directory after a full build |
| Repeated installation is idempotent | The CLI skips a skill already installed from the same source and ref |
| The guidance in a skill is good, and an agent follows it | **Review** |

The last row is a review obligation and stays one. A build can check that a skill is well formed; it
cannot check that prose is correct or that an agent obeyed it. See
[Agent Guidance Skills](Agent-Guidance-Skills.md) for the scenarios used to review that by hand.

**`[InheritsTests]` is load-bearing** on the derived test class. TUnit does not walk base types during
discovery, so without it the class compiles, the run is green, and nothing is validated — the worst
failure this arrangement could have.

## Using it

```bash
npx skills add purview-dev/engineering-playbook              # install
npx skills add purview-dev/engineering-playbook#v2.1.0       # pinned
npx skills list                                              # what is installed
npx skills update                                            # newest published
npx skills remove <name>
npx skills experimental_install                              # restore from skills-lock.json
```

Through the SDK, so a repository exercises the path it ships to consumers:

```bash
dotnet msbuild <project> -t:ListPurviewSkills                 # diagnostics; installs nothing
dotnet msbuild <project> -t:InstallPurviewSkills -p:PurviewSkillsSource=purview-dev/engineering-playbook
```

| Property | Default | Purpose |
| -- | -- | -- |
| `PurviewSkillsSource` | *(empty)* | Install from an explicit git source; append `#<ref>` to pin |
| `PurviewSkillsAgents` | `*` | Which agents to install to |
| `PurviewSkillsRunner` | `npx` | Which package runner launches the CLI |
| `PurviewSkillsCommand` | *(detected)* | Overrides the launcher entirely, skipping detection |
| `PurviewSkillsInstallRoot` | *(resolved)* | Forces the repository root |

### One repository, several skills

A repository publishes the whole of its `skills/` folder — there is no per-project subset any more,
because there is no package to subset. Scope is expressed in the **names**:

```text
skills/
├── purview-results-core
├── purview-results-union-errors
├── purview-results-http-mapping
├── purview-results-zodsharp-validation
└── purview-results-zodsharp-problems
```

Guidance about working *in this repository* — its test runner, its commit conventions — does not
belong in `skills/` at all. A skill here ships to somebody else; put repository-local guidance in
`AGENTS.md`.

### Troubleshooting

| Symptom | Cause |
| -- | -- |
| `PRSKL0004` | Neither `skills` nor a package runner is on `PATH`. Install Node.js, or `npm install -g skills`. `ListPurviewSkills` still works without it. |
| `PRSKL0005` | A skill is malformed: no `SKILL.md`, no frontmatter, no `name`/`description`, a name that does not match its folder, or a broken `references/` link. |
| `PRSKL0003` | The repository root could not be resolved for an install. Pass `PurviewSkillsInstallRoot`. |
| "No skills found" from the CLI | The source repository has no **root** `skills/` folder. Nested and dotted folders are not discovered. |
| A skill installed but the agent ignores it | Check the `description`. It is the trigger, and a topic label does not match a situation. |
| `PRSKL0010`–`PRSKL0014` | Recommendation diagnostics raised against packages built by an older SDK that still carried `.agents/skills/` metadata. |

## Publishing skills for a new library

1. Author the skill at the repository root: `skills/<name>/SKILL.md`.
2. Give it a vendor-and-product prefix.
3. Add `[InheritsTests] public sealed class SkillTests : PurviewSkillTests;` to a test project
   referencing `Purview.Skills.Testing`, so the rule set runs on every build.
4. Verify with `npx skills add <owner>/<repo> -l` — if it reports no skills, the folder is in the
   wrong place.

## At your scale

- **One repository, one agent.** You do not need any of this. Put `SKILL.md` files in `skills/` and
  run `npx skills add .` when they change. The format is the part worth adopting first.
- **Several repositories sharing conventions.** Publish from the repository that owns the convention
  and install with the CLI. Pin the ref; commit `skills-lock.json`.
- **A library with consumers.** Same mechanism, plus a named prefix and the conformance test, so a
  broken skill fails in the repository that owns it rather than in somebody else's agent.
- **A platform with many repositories.** The thing to copy is the **separation**: authoring and
  validation in the owning repository, installation in a tool that owns agent paths, and exactly one
  writer per path.

## See also

- [Agent Enablement](Agent-Enablement.md) — `AGENTS.md`, `.agents/`, and what makes a good skill
- [Documentation Convention](Documentation-Convention.md)
- [skills CLI](https://github.com/vercel-labs/skills)
- [Agent Skills specification](https://agentskills.io/specification)
