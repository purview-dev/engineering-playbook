# Agent Enablement

## The rule

A repository tells an agent what it is, how it is laid out, and what its conventions are — in the
repository, in files an agent reads without being pointed at them.

Four paths do the work:

| Path | What it is |
| -- | -- |
| `AGENTS.md` | The repository-wide source of truth for agents. Purpose, authority, layout, conventions, local commands, completion checklist. |
| `.github/copilot-instructions.md` | One job: defer to `AGENTS.md`. |
| `skills/` | The skills this repository authors and publishes. Tracked. |
| `.agents/skills/` | Skills installed from other repositories by the skills CLI. Gitignored; `skills-lock.json` is the tracked record. |

The rule has a corollary that is easy to miss: **nothing important may be discoverable only by reading the
code.** If the answer to "which of these two patterns is the intended one?" lives in the commit history or
in a colleague's head, the repository is not legible, and an agent will pick the wrong one at a rate that
looks like randomness.

## Why

A coding agent has the repository and nothing else. It has no colleague to ask, no institutional memory, no
sense of which of two plausible patterns the team settled on, and no way to tell a deliberate pattern from
an accident. It will confidently generalise from whatever it finds first.

This is not really an agent problem. Every failure mode above is also a new-joiner failure mode, and the
guidance that fixes it is the same guidance: state the purpose, draw the layout, name the conventions, and
write down the definition of done. The agent makes the cost visible sooner, because an agent does not ask.

## `AGENTS.md`

It should answer, in this order:

1. **Purpose and authority.** What the repository contains, that this file is the source of truth, that a
   more specific `AGENTS.md` in a subtree takes precedence, and what the agent must not do (operate outside
   the repository, read or commit secrets).
2. **Source of truth and layout.** A table of path → purpose. This is the single highest-value section: it
   is what stops an agent inventing a folder.
3. **Conventions.** The rules that are not derivable from the code — test naming, where domain content comes
   from, which project owns which concern.
4. **Local commands.** The `just` recipes, and what each is for.
5. **Completion checklist.** What "done" means: what to update, what to run, and what to state about the
   validation that ran.

Two properties matter more than completeness:

- **It is a table of contents, not an encyclopedia.** Long guidance belongs in the wiki, where it is
  published and reviewed; `AGENTS.md` points at it.
- **It is specific.** "Follow established patterns" is not guidance. "Domain content in a test comes from
  `TestBuilder`; never construct a domain type inline" is guidance.

`AGENTS.md` and the wiki cross-reference each other deliberately. A rule that only an agent is told is a
rule the humans have not written down, and a rule that only the wiki states is one the agent will not find.

## The `.agents/` folder

```text
skills/                 skills this repository authors, tracked in git
└── <skill-name>/
    ├── SKILL.md        the skill itself
    └── references/     optional deeper material the skill links to

.agents/
└── skills/             skills installed from other repositories, by the skills CLI; gitignored
    └── <skill-name>/

skills-lock.json        the committed record of what the CLI installed
```

Note the split, and that it is a **single-writer** split. A skill you **author** lives at the
repository root in `skills/`, where it is tracked and where a cloning installer can find it. A skill
you **receive** from somewhere else arrives in `.agents/skills/`, written by the CLI and gitignored.

The same folder cannot do both jobs. `.agents/skills/` is the CLI's universal install location, so a
repository that also mirrored its own skills there would have two writers on one set of paths — and
the build would silently overwrite a pinned install.

Link to a skill by its **authored** path, `skills/<name>/SKILL.md`. A link into `.agents/skills/`
points at a gitignored file and is broken in a fresh clone.

A **skill** is a self-contained instruction set for one kind of task. It opens with YAML frontmatter
whose `description` is the trigger — it is what tells an agent that this skill applies:

```markdown
---
name: purview-persistence
description: >-
  Use when a .NET project actually persists data and you are choosing a storage approach, deciding
  whether to abstract EF Core, or selecting a GUID primary-key strategy.
---
```

Writing a good trigger is most of writing a good skill. "Use when X, and you need Y" is far more
useful than a topic label, because the agent is matching a situation, not searching an index.

Prefer a skill to a prompt, and a skill to a shipped sub-agent definition. A prompt or a mirrored
agent file is inert: it lands somewhere no agent reads natively, so somebody has to already know it
exists. A skill carries a `description` an agent matches against the task, so it fires when it is
relevant. Anything task-shaped and reusable belongs in `skills/`.

And guidance about working *in this repository* belongs in `AGENTS.md`, not in a skill — a skill
ships to somebody else.

## Getting somebody else's guidance

Install it, explicitly:

```bash
npx skills add purview-dev/build-sdk
npx skills add purview-dev/engineering-playbook#v2.1.0    # pinned
```

The CLI writes into each agent's own directories and records the source and ref in
`skills-lock.json`, which is committed. Everything it writes is gitignored, because it is all
reproducible from the lockfile with `npx skills experimental_install`.

> An earlier arrangement had the SDK mirror a package's agent content into `.agents/` on build. It
> was removed in `Purview.BuildSdk` 1.0.6: it duplicated the CLI, wrote to the same paths, and made
> an SDK upgrade capable of replacing a pinned install.
> [Agent Skills Distribution](Agent-Skills-Distribution.md) has the reasoning.

The SDK still keeps its own state in `.purview/`, whose `.gitignore` is a single `*` — so a cache
never appears in a status output.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| An authored skill is well formed, correctly named, and its links resolve | `ValidatePurviewSkills` at pack time, and `PurviewSkillTests` on every test run |
| An empty `skills/` folder is reported rather than passing vacuously | `PurviewSkillTests` discovery rule |
| A build installs nothing into an agent directory | Test asserts no lockfile and no agent directory after a full build |
| Installed skills are reproducible | `skills-lock.json`, committed; everything the CLI writes is gitignored |
| The SDK's cache never appears as a change | `.purview/.gitignore` |
| `AGENTS.md` exists, is accurate, and is specific | Review |
| `copilot-instructions.md` defers to `AGENTS.md` | Review |

## At your scale

- **Any repository.** Write `AGENTS.md`. It is the cheapest thing on this page and the one with the
  largest effect on an agent's first ten minutes.
- **A repository with conventions worth repeating.** Add a skill. The test is whether you have
  written the same explanation twice in review comments.
- **A repository others depend on.** Publish your skills from it, with a vendor-and-product name
  prefix, and validate them in the test suite so a broken one fails where it was written. See
  [Agent Skills Distribution](Agent-Skills-Distribution.md).
## See also

- [Agent Skills Distribution](Agent-Skills-Distribution.md)
- [Documentation Convention](Documentation-Convention.md)
- [Repository Shape](Repository-Shape.md)
- [Agent Skills specification](https://agentskills.io/specification)
