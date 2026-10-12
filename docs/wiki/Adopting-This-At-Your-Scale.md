# Adopting This at Your Scale

Every rule in this playbook came from a repository that needed it. That is the argument for the rule and
also the reason not to take all of them: a rule adopted before the problem it prevents exists is pure cost.

This page is the short version of that decision. Find the profile that matches, take what it lists, and
leave the rest — with the knowledge that the rest is there when the repository grows into it.

## The three profiles

| | Small | Medium | Large |
| -- | -- | -- | -- |
| **Shape** | One project | A few layers or modules | Several packages |
| **Repository** | `src/src/<Project>`, one test project | `src/src/*` + `src/tests/*` + a shared testing project | As medium, plus `Sdk/` assets per package and `samples/` |
| **SDK** | Yes | Yes | Yes |
| **Test data builder** | Yes, from the first test | Yes | Yes |
| **Architecture tests** | No | Yes | Yes, and promoted into the build |
| **CI tiers** | One | Two (`Unit`, plus one slow tier) | Several, with a release gate |
| **Pack validation** | n/a | Only if it packs | Yes, exhaustively |
| **Consumer verification** | No | No | Yes |
| **Wiki** | `index`, `Home`, `_Sidebar`, one page | One page per public concern | One page per subsystem, plus a hub |
| **`AGENTS.md`** | Yes | Yes | Yes |
| **Agent skills** | The SDK's mirrored set | Plus one per repeated review comment | Plus your own packaged skills |

## Small — one project

Adopt:

- The SDK import in `Directory.Build.props` and a pinned `global.json`. This is the single highest-value
  change in the whole playbook: it removes the target framework, language version, nullable context, analyzer
  set and package metadata from the project file, and it does so in two lines.
- `Directory.Packages.props` with the versions the SDK injects, or restore fails with `NU1010`.
- `AGENTS.md`. The cheapest thing here, and the one with the largest effect on an agent's first ten minutes.
- `TestBuilder` in the test project, from the first test. Retrofitting it later is a mechanical edit across
  every file, and it is the kind of edit that is postponed forever.
- `docs/wiki/index.md`, `Home.md` and `_Sidebar.md` — three small files that make the project documentable.

Skip:

- Architecture tests. With one project there is no direction to assert. Adopt them the moment a second
  project exists, because that is when the wrong direction first becomes possible.
- CI tiering. Everything is a unit test, so there is nothing to tier.
- Pack validation, consumer verification, release channels. All three need a published package to mean
  anything.

## Medium — a few layers

Everything from Small, plus:

- **Architecture tests.** Rules 1–3 from [Architecture as Tests](Architecture-As-Tests.md). A page of code,
  and it pays for itself the first time someone adds a reference "just to reuse a helper".
- **A shared testing project.** Once two test projects need the same builder or fixture, `SharedTestingInfra`
  gives them one home and the SDK references it automatically.
- **A second CI tier.** The moment a test needs a real resource, give it its own project and its own job.
  Keep the fast tier always on; make the slow tier explicit.
- **A wiki page per public concern**, plus a getting-started page that is the shortest path to a working
  example.

This is the profile the reference implementation in this repository is built to. It is the smallest shape
where every mechanism in the playbook is visible.

## Large — several packages

Everything from Medium, plus:

- **Pack validation.** An exhaustive manifest of what each package ships. It is the only mechanism that stops
  a package shipping without the asset it documents, and the failure is otherwise found by a consumer.
- **Consumer verification.** A script that packs, then builds throwaway consumers against the produced
  packages, asserting the documented happy path and the documented guard errors. This is the tier that
  catches the drift between a package's contract page and the package.
- **A release gate.** The slow tier runs before a release, not only on a pull request.
- **Release channels** rather than a second repository for prereleases.
- **Your own agent skills**, packaged so the guidance travels with the code.
- **Architecture rules promoted into the build.** At this size a rule that can be skipped is a rule that
  will be, so move it from a test into an analyzer or an MSBuild check that runs before compile.

## What not to adopt early

Four things that are more expensive than they look when taken out of order.

**Architecture tests before there is an architecture.** A test asserting that "the domain does not reference
the web layer" is meaningless in a repository with one project, and it teaches the team that architecture
tests are ceremony.

**A test data builder after the tests.** Retrofitting `TestBuilder` means editing every test file at once,
which means a large diff nobody reviews carefully. Adopt it with the first test, when the diff is one file.

**A pack-validation manifest before the first package.** `RequireExplicitContent` defaults to `true`, so a
repository that packs anything without a matching rule gets an error. That is the right behaviour — and it is
the wrong thing to discover while adding the first package. Turn on pack validation with the package.

**Consumer verification before there is a consumer.** The script exists to keep a published contract honest.
With no published contract it is a slow test that restores from nuget.org and proves nothing.

## Migrating an existing repository

The order matters, because each step makes the next one cheaper.

1. **`global.json` and the SDK import.** Do this first and in isolation. Expect the build to surface the
   style and analyzer diagnostics that the repository has been accumulating, and deal with them as a
   deliberate pass — that pass is most of the value.
2. **`Directory.Packages.props`.** Add the versions the SDK injects. This is a mechanical fix for `NU1010`.
3. **Renames.** Move projects to the conventional folders and names, one at a time, and fix the namespace
   each rename implies. Do this before adding architecture tests, because those tests will otherwise fail for
   reasons that are about naming rather than architecture.
4. **`AGENTS.md`.** Write it from what the repository already does, not from what it should do. An accurate
   description of the current state is more useful than an aspiration, and it makes the gaps visible.
5. **Architecture tests.** Now they can express the layering you actually have.
6. **`TestBuilder`.** Introduce it beside the existing inline construction, convert one test project at a
   time, and delete the old path when the last file is converted.
7. **CI tiering and the wiki.** Last, because both depend on the shape being settled.

Two things to do at every step: keep the build green, and write down the rule you just adopted on the page
where it belongs. A migration that leaves the conventions undocumented is a migration that will be undone.

## See also

- [Getting Started](Getting-Started.md)
- [Engineering Principles](Engineering-Principles.md)
- [Worked Examples](Worked-Examples.md)
- [Contributing](Contributing.md)
