# Documentation Convention

## The rule

The wiki lives in the repository it describes, in `docs/wiki`, and the repository owns its `mkdocs.yml`,
its navigation and its `edit_uri`.

| Path | Purpose |
| -- | -- |
| `docs/wiki/index.md` | The landing page. Short, with links in. |
| `docs/wiki/Home.md` | The hub: what the project is, how to read a page, and a table linking every page. |
| `docs/wiki/_Sidebar.md` | The navigation list. The leading underscore keeps it out of the content listing. |
| `docs/wiki/<Page-Name>.md` | One page per concern. Title case, hyphens between words. |
| `mkdocs.yml` | Site configuration; `docs_dir: docs/wiki`. |
| `README.md` | The repository's front door on GitHub. Points into the wiki; does not duplicate it. |
| `AGENTS.md` | The agent's front door. Points into the wiki; does not duplicate it. |

## Why

**Ownership.** Documentation that lives outside the repository it describes is updated outside it. A page
with no owner and no review path is wrong within one release, and nobody can tell when it happened.

**Discoverability has two halves.** `_Sidebar.md` is what the aggregating site builds its navigation from,
and `Home.md` is what a reader lands on. A page in neither is a page that exists only for people who
already know its URL — which is to say, for nobody.

**Three front doors, three audiences.** `README.md` is for someone deciding whether to use the thing.
`Home.md` is for someone who has decided and needs to know where to look. `AGENTS.md` is for an agent that
needs the layout and the conventions. They link to the same pages and should not restate them; a fact
stated twice is a fact that will disagree with itself.

## `mkdocs.yml`

```yaml
site_name: Acme
site_description: Developer documentation for Acme
repo_url: https://github.com/acme/acme
edit_uri: edit/main/docs/wiki/
docs_dir: docs/wiki

theme:
  name: material
  palette:
    - media: "(prefers-color-scheme: light)"
      scheme: default
      primary: indigo
      accent: cyan
    - media: "(prefers-color-scheme: dark)"
      scheme: slate
      primary: indigo
      accent: cyan
  features: [navigation.instant, navigation.sections, navigation.top, search.highlight, search.suggest, content.code.copy]

plugins: [search, techdocs-core]
markdown_extensions: [admonition, attr_list, md_in_html, pymdownx.details, pymdownx.superfences]
```

Three of those lines are load-bearing:

- **`edit_uri`.** It is the "edit this page" link, and it is the only mechanism that turns a reader into a
  contributor. Get the branch wrong and the link 404s.
- **`docs_dir: docs/wiki`.** The wiki is a folder inside the repository rather than the repository root, so
  the root stays available for the things that belong there.
- **`plugins: [search, techdocs-core]`.** `techdocs-core` is what the aggregating portal expects. Without
  it the site builds and the portal does not pick the pages up.

> **Mermaid is not enabled.** `pymdownx.superfences` is configured without a Mermaid custom fence, so a
> ` ```mermaid ` block renders as unstyled text. Raw Mermaid source in a ` ```text ` block is the honest
> workaround: it is copy-pasteable into any renderer, and it does not pretend to be a diagram.

## How to write a page

Every content page uses the same four sections, in this order:

| Section | Answers |
| -- | -- |
| **The rule** | What is expected. |
| **Why** | The failure the rule prevents. |
| **How it is enforced** | The mechanism that makes it true — or an explicit statement that it is a review obligation. |
| **At your scale** | What to do when the repository is smaller or larger than the one the rule came from. |

Three pages are deliberately not content pages. `Home.md` is the hub and `index.md` is the landing
page, so both are navigation rather than rules. `Engineering-Principles.md` is an index: it states each
of the ten rules in one line and links to the page that states it in full, so a rule is never written
twice.

The shape is not decoration. It forces three questions that a page written as prose usually skips: *is this
actually a rule, or an opinion?*; *what breaks if we drop it?*; and *is it enforced, or are we asking a
reviewer to remember?*

The third section is the one that earns the most. Labelling something a review obligation is
uncomfortable, and that discomfort is the point: it is how a team discovers that half its "conventions" are
aspirations. A rule that claims to be enforced when it is not is worse than no rule, because people rely on
the guarantee and stop looking.

The fourth section is what makes a playbook reusable. A rule that only makes sense at one size is a rule
that gets cargo-culted at another. Naming the smaller version explicitly — *"one tier is fine until the slow
tests exist"* — is what lets a reader take the part that fits.

## Two habits that keep it accurate

**Link to the authority instead of paraphrasing it.** The SDK's own wiki is the authority on what the SDK
does. A paraphrase in this playbook is a second source of truth, and the two will drift. Where a rule is the
SDK's, link to it; where a rule is a consequence of the SDK's behaviour that is not written down anywhere
else, state it here and say why.

**Say which repository the evidence came from.** Every rule in this playbook came from a real repository and
a real failure. Naming the repository turns a claim into something a reader can check against running code,
and it is the fastest way for a reader to decide whether the rule applies to them.

## Writing style

- **ATX headings (`##`), one `#` per page.** The `#` is the page title; the navigation already knows it.
- **Code fences always declare a language.** ` ```csharp `, ` ```xml `, ` ```json `, ` ```yaml `,
  ` ```text ` for output and diagrams. An unlabelled fence renders as a plain block and loses highlighting.
- **Tables for anything enumerable.** A rule list, an input list or a mapping reads better as a table than
  as five paragraphs, and it survives editing.
- **Short lines.** A page is read in a diff as often as in a browser. Wrapping at roughly 110 columns keeps
  a documentation change reviewable.
- **British spelling in prose, exact spelling in identifiers.** `normalise` in a sentence, `Normalize` if
  that is the API's name. Never "fix" a name to match the prose.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| The site configuration is present and points at `docs/wiki` | Review |
| Every content page is in `_Sidebar.md` and the `Home.md` table, and their links resolve | `DocumentationTests` |
| Pages follow the four-section shape | Review |
| Fences declare a language | `DocumentationTests` |
| `README.md` and `AGENTS.md` point in rather than duplicate | Review |

The navigation and the fences are the two conventions a machine can check, and they are the two that
break silently: a page nobody can reach is a page that does not exist, and a bare fence renders as an
untyped block. Both are enforced by `DocumentationTests` in `src/tests/Repository.UnitTests`. The rest
is a review obligation, and this page says so rather than implying otherwise.

## At your scale

- **One page.** `docs/wiki/index.md` plus `Home.md` and `_Sidebar.md`, and a `mkdocs.yml`. Three small files.
- **A library.** One page per public concern, plus a getting-started page that is the shortest path to a
  working example. Keep the consumer contract on its own page — it is the page that must not drift.
- **A platform.** Add a page per subsystem, and keep a single hub that links them all. The hub is what stops
  a wiki becoming a search problem.
- **Several repositories.** Keep each wiki with its code, and let one site aggregate them. The aggregation
  is a build concern; ownership stays with the repository.

## See also

- [Home](Home.md)
- [Agent Enablement](Agent-Enablement.md)
- [Contributing](Contributing.md)
