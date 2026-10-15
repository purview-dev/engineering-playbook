# Engineering Principles

These are the ten rules the Purview repositories are held to. This page is an index: each rule is
stated once, in full, on the page named beside it. That page carries the failure the rule prevents, the
mechanism that makes it true, and what a smaller repository does instead — so a rule is never written
twice and the two copies cannot disagree. Where a rule has no dedicated page, the line here is its full
statement.

The distinction that runs through all ten: **a convention a build can check is checked by the build; a
convention only a reviewer can check is labelled a review obligation and never described as enforced.**
A rule that claims enforcement it does not have is worse than no rule, because people stop looking.

| # | Principle | In one line | Detail |
| -- | -- | -- | -- |
| 1 | Convention over configuration | A repository declares as little as possible; one MSBuild SDK supplies the defaults, so a project file is short. | [Repository Shape](Repository-Shape.md) |
| 2 | Identity is derived, never repeated | A name appears once, in the folder and file name; `RootNamespace` and `AssemblyName` are computed from it. | [Repository Shape](Repository-Shape.md) |
| 3 | Architecture is a test, not a diagram | The dependencies a layer may have are asserted by tests, so a wrong `ProjectReference` fails the build. | [Architecture as Tests](Architecture-As-Tests.md), [Project Responsibilities](Project-Responsibilities.md) |
| 4 | The build is the only authority | A convention a build can check is checked by the build; the rest is labelled a review obligation, never described as enforced. | [Documentation Convention](Documentation-Convention.md) |
| 5 | Warnings are not acceptable output | A clean build has no warnings: fix one, suppress it where it occurs with a justification, or turn the rule off deliberately. | [Testing and Test Data](Testing-And-Test-Data.md) |
| 6 | Every claim about a consumer is proved by a consumer | A package that documents how it must be used ships something that builds that documented consumer and asserts the documented outcome. | [Contracts and Data Exchange](Contracts-And-Data-Exchange.md) |
| 7 | Documentation is a build artefact of the repository | The wiki lives in the repository it describes, and the repository owns its `mkdocs.yml`, navigation and `edit_uri`. | [Documentation Convention](Documentation-Convention.md) |
| 8 | Everything a test needs is built by one builder | Domain content in a test comes from `TestBuilder`; a test overrides only the value it is proving. | [Testing and Test Data](Testing-And-Test-Data.md) |
| 9 | An agent is a reader with no memory | A repository states its purpose, layout and conventions in `AGENTS.md`; nothing important is discoverable only from the code. | [Agent Enablement](Agent-Enablement.md) |
| 10 | A change is verified in proportion to its risk | Checks are tiered; a pull request runs the fast tier, and the slow tier is run deliberately. | [Verification Model](Verification-Model.md) |

Enforcement is stated per rule on the linked page. Three are enforced by the build in a way worth
knowing at a glance: identity (rule 2) by `PurviewProjectFileNameMismatch` and the SDK's derivation,
architecture (rule 3) by the architecture tests, and the verification tiers (rule 10) by the pipeline's
`TestFilter`. The rest are enforced partly by the build and partly by review, and each page says which.

## At your scale

Every rule has a smaller version, and taking one before the problem it prevents exists is pure cost.
[Adopting This at Your Scale](Adopting-This-At-Your-Scale.md) decides which rules apply to a small,
medium or large repository, and in what order to adopt them.

## See also

- [Repository Shape](Repository-Shape.md)
- [Architecture as Tests](Architecture-As-Tests.md)
- [Verification Model](Verification-Model.md)
- [Testing and Test Data](Testing-And-Test-Data.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
