# Engineering Principles

These are the rules the Purview repositories are held to, each with the failure it prevents and the
mechanism that makes it true. Most of them are not stated here for the first time: the authoritative
statement of the SDK's own behaviour is the [build-sdk wiki][build-sdk], and this page links to it rather
than paraphrasing it, because a paraphrase is a second source of truth that drifts.

## 1. Convention over configuration

**The rule.** A repository declares as little as possible. Defaults come from one MSBuild SDK, and a
project file is short because the SDK already knows what a project of that shape needs.

**Why.** Configuration that is copied between repositories diverges. When the rule lives in one package,
fixing it once fixes it everywhere; when it lives in eleven project files, it is fixed eleven times or,
more often, once.

**How it is enforced.** `<Import Sdk="Purview.BuildSdk" Project="Sdk.props" />` in a single
`Directory.Build.props`. The SDK detects project type from the filename and the `Sdk` attribute, and
applies the corresponding defaults. See [build-sdk: Project Type
Detection](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Project-Type-Detection.md).

**At your scale.** A single-project repository still benefits: the SDK import is two lines and removes
the target framework, language version, nullable context, analyzer set and package metadata from the
project file.

## 2. Identity is derived, never repeated

**The rule.** `RootNamespace` comes from `$(NamespacePrefix).$(ProjectName)`, and `AssemblyName` follows
the resolved `RootNamespace`. A name appears once, in the folder and file name, and everything else is
computed from it.

**Why.** A namespace that is typed by hand drifts from the assembly it lives in, and the drift is
invisible until something reflects over types — an architecture test, a serializer, a DI scan.

**How it is enforced.** The SDK derives both during `.props` evaluation, and a build-time check
(`PurviewProjectFileNameMismatch`) fails when a `.csproj` filename does not match its parent directory.

**At your scale.** Always. This rule costs nothing and removes a class of bug.

## 3. Architecture is a test, not a diagram

**The rule.** The dependencies a layer is allowed to have are asserted by tests that read the compiled
assemblies. If a `ProjectReference` points the wrong way, the build fails.

**Why.** A diagram is a claim about the past. Dependency direction erodes one convenient reference at a
time, and every such reference looks reasonable in isolation.

**How it is enforced.** A test project that references every layer and inspects their metadata
references. See [Architecture as Tests](Architecture-As-Tests.md), and the running example in
`src/tests/Architecture.UnitTests`.

**At your scale.** Small repositories can skip it. Adopt it the moment a second layer exists, because
that is when the wrong direction first becomes possible.

## 4. The build is the only authority

**The rule.** A convention that a build can check is checked by the build. A convention that only a
reviewer can check is labelled as a review obligation and never described as enforced.

**Why.** A rule that claims to be enforced when it is not is worse than no rule: people rely on the
guarantee and stop looking. The distinction has to be visible in the documentation, not inferred.

**How it is enforced.** Style is enforced by analyzers and `EnforceCodeStyleInBuild`; the SDK's own style
policy is re-validated on every compile, so a repository cannot quietly downgrade a rule. See
[build-sdk: SDK-Shipped
Analyzers](https://github.com/purview-dev/build-sdk/blob/main/docs/wiki/Analyzers.md).

**At your scale.** Always. If a rule matters and cannot be checked, say so plainly.

## 5. Warnings are not acceptable output

**The rule.** A clean build has no warnings. A warning that will never be fixed is either fixed, or
suppressed where it occurs with a stated justification, or the rule is turned off deliberately.

**Why.** Warnings are a budget. A build that emits twenty is a build where the twenty-first is invisible,
and the twenty-first is the one that mattered.

**How it is enforced.** Partly by severity: the SDK's shipped `.editorconfig` sets many rules to `error`,
so the important ones cannot be ignored. The rest is a review obligation, backed by the pipeline running
`dotnet build` with the repository's configuration.

**At your scale.** A small repository can tolerate `warning`; a large one cannot. Either way, decide
deliberately rather than by accumulation.

## 6. Every claim about a consumer is proved by a consumer

**The rule.** When a package documents how it must be used — a target framework, an asset, a guard, a
workaround — something in the repository builds that documented consumer and asserts the documented
outcome.

**Why.** Consumer-facing documentation is the part of a library least likely to be exercised by its own
tests, because the library's own build is not a consumer. It drifts silently and fails in the consumer's
repository rather than ours.

**How it is enforced.** A dedicated verification script that packs, then builds throwaway consumer
projects against the produced packages, asserting both the happy path and the documented guard errors.
[containers](https://github.com/purview-dev/containers) is the worked example: `just verify-consumers`
asserts every claim in its consumer-requirements page, and the script is deliberately excluded from the
default unit filter because it restores from nuget.org.

**At your scale.** Only worth it once a package is consumed outside its own repository. Before that, the
repository's own tests already are the consumer.

## 7. Documentation is a build artefact of the repository

**The rule.** The wiki lives in the repository it describes, in `docs/wiki`, and the repository owns its
`mkdocs.yml`, its navigation and its `edit_uri`.

**Why.** Documentation that lives elsewhere is updated elsewhere. A page with no owner and no review path
is wrong within one release.

**How it is enforced.** Review, plus the navigation file: a page that is not in `_Sidebar.md` is not
published by the aggregating site. See [Documentation Convention](Documentation-Convention.md).

**At your scale.** Always. Even a repository with one page should own that page.

## 8. Everything a test needs is built by one builder

**The rule.** Domain content in a test comes from `TestBuilder`. A test never constructs a domain object
inline, and it overrides only the value it is proving.

**Why.** Inline construction encodes the shape of a type into every test that touches it, so one
constructor change becomes a repository-wide edit. It also hides which values the test actually depends
on, because every value looks deliberate.

**How it is enforced.** Review. See [Testing and Test Data](Testing-And-Test-Data.md).

**At your scale.** Adopt from the first test. Retrofitting it is a mechanical edit across every test file,
and it is the kind of edit that is postponed forever.

## 9. An agent is a reader with no memory

**The rule.** A repository states its purpose, its layout and its conventions in `AGENTS.md`, and ships
the same guidance the build uses. Nothing important is discoverable only by reading the code.

**Why.** A coding agent has the repository and nothing else — no colleague to ask, no institutional
memory, no intuition about which of two plausible patterns is the intended one. The guidance that makes
the repository obvious to an agent is the same guidance that makes it obvious to a new joiner.

**How it is enforced.** Review, plus the SDK mirroring its own skills into `.agents/` so the agent-facing
guidance tracks the SDK version rather than the repository's memory of it. See [Agent
Enablement](Agent-Enablement.md).

**At your scale.** Always. `AGENTS.md` is cheaper than the questions it prevents.

## 10. A change is verified in proportion to its risk

**The rule.** Checks are tiered, and a repository declares which tiers its pipeline runs. A pull request
runs the fast tier; the slow tier is run deliberately, locally or on a dedicated runner.

**Why.** A pipeline that runs everything on every pull request gets made optional. A pipeline that runs
too little gets ignored. The tiering is what keeps the gate credible.

**How it is enforced.** The pipeline's `TestFilter`, and the test project's own name — the SDK
categorises a `*UnitTests` project as `Unit`, so the tier is a property of the project rather than of a
hand-written attribute. See [Verification Model](Verification-Model.md).

**At your scale.** One tier is fine until the slow tests exist. Then split by project name, not by
attribute.

## See also

- [Repository Shape](Repository-Shape.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [Verification Model](Verification-Model.md)
- [Testing and Test Data](Testing-And-Test-Data.md)

[build-sdk]: https://github.com/purview-dev/build-sdk
