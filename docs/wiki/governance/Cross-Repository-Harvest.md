# Cross-Repository Harvest

## The rule

Every convention in this playbook was harvested from a **named, public `purview-dev` repository, read
at `main`**. The remote is the authority; a local clone is a cache, and it may be stale, renamed or
missing.

`.config/org-inventory.json` records what was read: the organisation, the branch, when it was fetched,
and a commit SHA per repository. It is generated, never hand-edited.

| Field | Holds |
| -- | -- |
| `org`, `branch` | `purview-dev`, `main` — the organisation and the branch a convention is read at |
| `fetchedAt` | When the snapshot was taken. One date for the whole file, so staleness is one comparison |
| `repositories[].commitSha` | The commit the claims about that repository were true at |
| `repositories[].visibility`, `archived` | Public and unarchived, or the entry does not belong here |
| `repositories[].docsDir`, `docsPages` | Where the documentation lives, and how much of it there is |
| `repositories[].skills` | The skill folders it authors |
| `repositories[].agentGuidance` | The agent-facing files it publishes |
| `repositories[].concerns` | The concerns it is best-in-class for |

**Private repositories are inputs, never evidence.** They may inform a maintainer's judgement; they
are never cited, never named and never recorded. Three mechanisms hold that line, and they fail in
different places:

- The writer **denies by default**: only a repository whose visibility is exactly `PUBLIC` and whose
  `archived` flag is exactly `false` reaches the file, so a visibility value nobody anticipated fails
  closed rather than falling through.
- The writer **refuses to persist** a snapshot that records anything else — including an entry a hand
  edit left behind, which a merge would otherwise carry forward.
- No file this repository publishes may name a repository the snapshot does not contain, so a private
  repository cannot be cited in a page, a skill, a workflow or a config and still pass.

## Why

**A local folder listing is not an inventory.** Two clones in a typical working directory here are
`eventsourcing` and `sourcegeneratorframework`; the repositories are `event-sourcing` and
`sourcegenerator-framework`. Four public repositories have no clone at all. An agent that globs the
parent directory, or a person reading it, gets a list that is short and partly misnamed — and every
entry in it looks authoritative, because it is a directory with code in it. This is the failure the
whole convention exists to prevent: the evidence was never read, only the neighbourhood.

**A renamed repository keeps resolving, so nothing catches it.** `Home.md` cited the catalogue
repository under its former name. That repository is now `purview-dev.github.io`. GitHub redirects,
`gh api` follows the redirect and answers 200 with the new name, and a reader who clicks the link
lands on a page that loads. The playbook was describing a name that no longer existed, and the only
symptom was one extra hop nobody could see. This page's repository-name test was written to catch
exactly that, and the correction to `Home.md` ships with it — a test with nothing to fail on is a
test nobody believes.

**A claim three pages agree on has not been checked.** `AGENTS.md`, `agents/Agent-Enablement.md` and
`foundations/Repository-Shape.md` all stated that `.agents/skills/` is gitignored. `git check-ignore
.agents/skills/x/SKILL.md` exits 1, and the SDK's own `.gitignore` template carries no such line: the
rule was stated in three places and existed in none. Repetition reads as verification, which is why a
harvest records a SHA rather than an impression. The rule is now in `.gitignore`, so the three pages
are true rather than merely consistent.

**An index maintained by hand under-counts silently.** The public catalogue in
`purview-dev.github.io` carries seventeen project files, one of which is archived, against seventeen
public live repositories, and the single repository without an entry is the catalogue's own: a
catalogue does not list itself. `openapi-xmlcomments` gained its entry after the previous harvest,
which is exactly how a hand-maintained index behaves — the omission is silent, and so is its
correction. A catalogue is a good reading order and a bad membership list. `gh repo list` decides
membership.

**A convention copied from the wrong repository is worse than none.** `build-sdk` owns what the
tooling does; this playbook owns why a repository is shaped that way. Reading either one for the
other's question produces an answer with a citation that does not support it.

## Deviations the first harvest found

The [Documentation Convention](../foundations/Documentation-Convention.md) says a wiki lives in `docs/wiki`. Of the
seventeen public repositories, thirteen do. `architests`, `value-objects` and `purview-dev.github.io`
publish from `docs`, and `pm-skills` publishes no documentation folder at all, documenting itself
through its eighteen skills.

That is not seventeen repositories getting a rule wrong; it is one repository's shape written down as if
it were universal. What the rule actually requires is that the documentation lives with the code — the
folder name is the part that varies, and nothing else does, because no repository carries a site
configuration any more (`astroc4` publishes `docs/wiki` and nothing beside it). The convention page now
says so, and names each deviation rather than pretending the rule was followed.

## How it is enforced

| Claim | Enforced by |
| -- | -- |
| The snapshot records public, unarchived repositories only | `OrgInventoryTests`; `inventory.mjs` refuses to write anything else |
| No published file names a repository the snapshot does not contain | `OrgInventoryTests` |
| Every skill declares the repository that publishes it | `OrgInventoryTests` |
| The script drops a private repository, refuses a private entry, and reads a BOM'd file | `InventoryScriptTests` |
| A page is reachable from the navigation, and its fences declare a language | `DocumentationTests` |
| The snapshot is current, and the analysis actually read `main` | **Review obligation** |

`InventoryScriptTests` runs the script itself as a subprocess — `node`, the script, arguments, no
shell — so the same assertions execute on Windows and on Linux, which is the point: the tooling is one
Node script rather than a PowerShell one and a Bash one that disagree. It replays a recorded membership
with `--from` and `--details` instead of faking `gh` on `PATH`; a shim was tried first and rejected,
because a non-shell spawn will not resolve a `.cmd` and the tests then reached the real `gh` and passed
against seventeen live repositories.

The second row is the one that catches a private repository by name, and it is deliberately blunt: it
reads every `.md`, `.cs`, `.json`, `.yml`, `.props` and `.mjs` file in the repository rather than the
wiki alone, because a skill is installed into somebody else's agent and a workflow file is a public
build log. A name that reaches either surface without passing through the script fails the build.

What no offline test can know is whether an entry's recorded visibility is a lie, because the truth
lives in the organisation rather than in the file. The compensating control is the drift report:
membership is re-read from `gh` rather than trusted from the file, so an entry that *stops* being
public prints `in the snapshot but no longer a public repository` the next time `just org-inventory`
runs.

The last row is the honest one. The snapshot is a claim about a moment, and a moment cannot be
enforced by a test that runs later: failing the build on an old `fetchedAt` fails whoever pushed next,
on a Tuesday, for a file they did not touch. So `just org-inventory` reports the drift and says when
the snapshot passed thirty days, and a reviewer decides.

The snapshot also trails this repository by one merge, by construction. It records the remote's SHA,
and a change is not on `main` until it is merged, so this repository's own row is always one commit
behind its tip. Regenerating the snapshot is part of merging a harvest, not part of making one.

## At your scale

- **One repository.** Keep a provenance list in `AGENTS.md`: one line per rule, naming the repository
  and the path it came from. No snapshot, no script — you have one copy of the truth and a name to
  check it against.
- **A few repositories.** A hand-written list is still enough, but date it. An undated list is read as
  current, which is the same failure with fewer steps.
- **A platform.** Commit the snapshot, generate it with a script, and assert in a test that the
  repositories your documentation names are in it. That test is cheap, offline, and the only rule
  here that can be enforced without a network call — which is why it is the one worth automating
  first.

## See also

- [Documentation Convention](../foundations/Documentation-Convention.md) — where a page lives, and why ownership is
  the rule rather than the folder name
- [Agent Skills Distribution](../agents/Agent-Skills-Distribution.md) — how a skill reaches a consumer
- [Engineering Principles](../Engineering-Principles.md) — the rules a harvest most often finds behind
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
