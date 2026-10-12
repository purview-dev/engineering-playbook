# Release Flow

## The rule

Three things, and the split between them is the whole design:

- **The version comes from `package.json`.** One file, one field, bumped by a maintainer in a pull request.
  No repository hand-writes a version into a project file, and no repository creates a release tag by hand.
- **The tool evaluates; the workflow decides.** The pipeline tool answers "may this version be released from
  this ref?" and reports the verdict. The workflow reads that verdict and sets the release mode. The tool
  never silently declines to release when it has been asked to.
- **A repository chooses a model and owns only its caller workflow.** The pipeline is shared.

## Why

**One version source.** A version that exists in two places is a version that disagrees with itself. Reading
it from `package.json` also means the version is visible to the thing that most often needs it — the
package metadata and the release notes — without a build step in between.

**Evaluate and decide are different jobs.** Eligibility rules are logic, and logic belongs somewhere it can
be unit-tested and run locally. A workflow is neither. But the *decision* has to stay in the workflow,
because a release is a repository-level act with repository-level consequences: the workflow is where the
concurrency group, the secrets and the branch protection live. Splitting them this way makes the rules
testable without making the release automatic.

**A shared pipeline.** Eleven repositories with eleven copies of a release script have eleven release
scripts, of which at least one is subtly wrong. One shared pipeline with eleven two-line callers has one.

## The models

A repository picks one. The choice is one setting plus its `on:` block.

| Model | Trigger | Policy | Used by |
| -- | -- | -- | -- |
| **A** — release on `main` | `push` to `main` | `ReleaseOnMain` (default) | Every package repository |
| **B** — main-as-head / release branch | `push` to `release` | `ReleaseOnMain` | Documented, not yet used |
| **C** — per-line release branches | `push` to `release/**` + `workflow_dispatch` | `TrunkReservesMinor` | A serviced stable line plus an in-flight prerelease line |

**Model A** is what almost every repository wants: each merge to `main` may release, and the tag that
already exists is what stops a second release of the same version. If the version in `package.json` is
already tagged, the verdict is `Skip` and the job finishes green having published nothing — which is the
correct outcome for a merge that did not bump the version.

**Model C** exists for the case where `main` must not ship: work accumulates, and a release is cut from a
`release/<line>` branch, so a stable line and a prerelease line advance independently.

## The rules

Eligibility rules are evaluated in ID order and short-circuit at the first non-passing one:

| Rule | Fails when |
| -- | -- |
| `REL001` | The version does not parse at the configured strictness. |
| `REL002` | The version is already tagged or published — this is a `Skip`, not a failure. |
| `REL003` | A stable version is released from a ref that is not a `StableRef`. |
| `REL004` | A non-zero `PATCH` is released from a ref that is not a `ServicingRef`. |
| `REL005` | The version is not greater than the line's highest. |
| `REL006` | A four-part version is used where four-part versions are not permitted. |
| `REL007` | The label does not match the channel's pattern. |

Three policies ship built in, so adopting a different model is one setting rather than a policy block to
copy:

| Policy | Rules | Refs |
| -- | -- | -- |
| `ReleaseOnMain` (default) | `REL001`–`REL003` | `StableRefs`: `refs/heads/main`, `refs/heads/release` |
| `TrunkReservesMinor` | `REL001`–`REL005` | `StableRefs`/`ServicingRefs`: `refs/heads/release/*`; `TrunkRefs`: `refs/heads/main` |
| `FourPartServicing` | `TrunkReservesMinor` + `REL006` | as above, plus `AllowFourPart` |

A disabled rule is skipped entirely rather than passing vacuously, which is why a Model A repository behaves
exactly as it always has.

## The caller workflow

A Model A package repository, in full:

```yaml
name: Release

on:
  push:
    branches: [main]

# Callers own release serialization; the reusable workflow defines no concurrency group.
concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  release:
    name: Release packages
    uses: purview-dev/build/.github/workflows/purview-release.yml@main
    with:
      release-mode: NuGet
      release-branch: main
    secrets: inherit
```

A repository that ships no package replaces `NuGet` with `GitHubRelease`, which creates the `v{version}`
tag and a GitHub release with generated notes and skips the publish step entirely. That is what this
playbook does:

```yaml
on:
  push:
    branches: [main]
    # A release is driven by a version bump, so the pipeline only wakes when package.json changes.
    paths:
      - package.json

concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  release:
    name: Tag and release
    uses: purview-dev/build/.github/workflows/purview-release.yml@main
    with:
      release-mode: GitHubRelease
      run-pack: false
      validate-pack: false
    secrets: inherit
```

Three details in those two files are load-bearing:

- **`concurrency` with `cancel-in-progress: false`.** The shared workflow deliberately defines no
  concurrency group, because serialisation is a repository decision: two releases racing each other is a
  worse outcome than a queued one.
- **`paths: [package.json]`.** Without it, every merge to `main` runs a release job that mostly decides
  `Skip`. With it, the job runs when the thing that can change the outcome changes.
- **`release-branch` is retained for compatibility.** Callers now own serialisation, so the input no longer
  gates anything.

## Seeing the decision without making it

`release-explain` prints the verdict and exits without running a single pipeline module:

```bash
purview-build release-explain --format=json
```

`--format=json` emits the stable contract the reusable workflow consumes — `verdict`, `releaseMode`,
`decidedByRule`, `message` — and `release-explain` always exits 0, because the decision is in the output
rather than in the exit code. The workflow reads the verdict and acts on it:

- `Release` → run the release pipeline with the evaluated mode.
- `Skip` → finish green with a notice. Nothing to do.
- `Fail` → fail the job, naming the rule that failed.

The same command works locally against simulated context (`Release:Context:Ref`, `ExistingTags`,
`PublishedVersions`), which performs no process or network lookups and says so in its report. A simulated
verdict that could be mistaken for a real one would be worse than no simulation.

> **The interlock.** If any `Release:Context:*` key is set and the run would really publish, the pipeline
> fails before any module runs. Simulation cannot leak into a real release.

## Channels

`Release:Channel` selects the feed and label policy, so a `preview` channel can publish to GitHub Packages
with `GitHubRelease: false` — preview packages without a GitHub release — while `stable` publishes to
nuget.org and tags. `Release:DryRun` runs everything and skips both publish steps, logging what each would
have done.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| One version source | The pipeline reads `package.json`; nothing else is consulted |
| A version is released at most once | `REL002`, checked against the tags that already exist |
| The version is bumped in a pull request | Review — the release job only reacts to what is already merged |
| A release cannot race another | The caller's `concurrency` group |
| Eligibility rules are correct | The tool's own test suite, including a release matrix over every scenario |

## At your scale

- **No package.** `GitHubRelease`, and a `paths: [package.json]` trigger. A release is a tag and notes.
- **One package.** Model A with `release-mode: NuGet`. Bump `package.json`, merge, and let the tag follow.
- **A preview line.** Add a channel rather than a second repository. `Release:Channel` is the mechanism; a
  second repository is not.
- **A serviced line.** Move to Model C and `TrunkReservesMinor`, which is the policy that exists to keep
  `main` from shipping a stable version while a line is serviced.

## See also

- [Verification Model](Verification-Model.md)
- [Engineering Principles](Engineering-Principles.md)
- [build: Release Flow](https://github.com/purview-dev/build/blob/main/docs/wiki/Release-Flow.md)
- [build: Release Models](https://github.com/purview-dev/build/blob/main/docs/wiki/Release-Models.md)
