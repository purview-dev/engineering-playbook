# Documentation Convention

## The rule

The wiki lives in the repository it describes, in `docs/wiki`, as plain Markdown. The repository owns **no
site configuration at all** — no theme, no plugin list, no navigation file to build — because the site
that publishes the pages lives in one place and reads them out of the repository.

| Path | Purpose |
| -- | -- |
| `docs/wiki/Home.md` | The hub: what the project is, how to read a page, and a table linking every page. |
| `docs/wiki/_Sidebar.md` | The navigation list, grouped by section. The leading underscore keeps it out of the content listing. |
| `docs/wiki/<Section>/<Page-Name>.md` | One page per concern, in the lower-case folder for its topic. Title case file name, hyphens between words. |
| `README.md` | The repository's front door on GitHub. Points into the wiki; does not duplicate it. |
| `AGENTS.md` | The agent's front door. Points into the wiki; does not duplicate it. |

**Folders are sections.** A content page lives in the topic folder its section names — this playbook uses
`foundations/`, `data/`, `testing/`, `agents/`, `governance/` and `examples/` — and the sidebar and the
`Home.md` tables carry those sections in the same order. A page's folder is part of its published URL, so
`foundations/Repository-Shape.md` is published under `foundations/`; move a page between folders only when
its topic changes, because every inbound link has to move with it. Only the navigation and entry pages stay
at the wiki root: `Home.md`, `_Sidebar.md`, and the getting-started and principles pages that the hub
links first. A wiki with one page needs no folders; add a folder when a topic has enough
pages that a flat list stops being readable.

**Scope.** This is the layout of a repository whose documentation is published, and `docs/wiki` is the
folder this playbook publishes from. Of the seventeen public repositories in the organisation, thirteen
use `docs/wiki`; `architests`, `value-objects` and `purview-dev.github.io` publish from `docs`, and
`pm-skills` publishes no documentation folder at all, documenting itself through its skills. What the
rule requires is the part those repositories share: the documentation lives with the code it describes,
as Markdown, with its navigation in the folder beside it. The folder name is the only part that varies.
[Cross-Repository Harvest](../governance/Cross-Repository-Harvest.md) records the deviations by name.

## Why

**Ownership.** Documentation that lives outside the repository it describes is updated outside it. A page
with no owner and no review path is wrong within one release, and nobody can tell when it happened.

**Discoverability has two halves.** `_Sidebar.md` is what the aggregating site builds its navigation from,
and `Home.md` is what a reader lands on. A page in neither is a page that exists only for people who
already know its URL — which is to say, for nobody.

**A flat folder becomes a search problem.** Twenty pages in one directory is a list nobody scans; the
same twenty under six topic folders is a map. The folder also puts the topic in the URL, so a link tells
the reader where in the playbook they are about to land.

**Three front doors, three audiences.** `README.md` is for someone deciding whether to use the thing.
`Home.md` is for someone who has decided and needs to know where to look. `AGENTS.md` is for an agent that
needs the layout and the conventions. They link to the same pages and should not restate them; a fact
stated twice is a fact that will disagree with itself.

## Plain Markdown, and nothing else

There is no site configuration in the repository: no theme, no plugin list, no `pip install`, no
lockfile beside the docs. A document is a `.md` file, and the tooling that renders it belongs to the
site rather than to the repository. That is not minimalism for its own sake — a second configuration
file is a second place for the navigation to be wrong, and a navigation that lives in a build tool's
file is edited by whoever last touched the build rather than by whoever last touched the pages.

Two consequences of owning no configuration:

- **`_Sidebar.md` is the whole navigation.** It is a document in the wiki, so it is reviewed with the
  pages it lists and a link that is dead is a link a reviewer can see. A page that is not in it is
  published nowhere that a reader can reach.
- **No diagram extension, no custom syntax.** A ` ```mermaid ` fence has nothing to render it, so raw
  Mermaid source goes in a ` ```text ` block — copy-pasteable into any renderer, and honest about being
  source rather than a picture.

## How the portal publishes a wiki

The public site reads the Markdown out of the repositories; there is no site to configure and nothing to
build. For each project it reads an entry in the catalogue (`src/src/data/projects/<id>.yml` in
`purview-dev.github.io`), fetches the Markdown that entry names, and renders it. The behaviour is
defined by that repository's own `docs-aggregation-rules` skill and its `src/src/lib/docs/`; what
follows is the part an author has to know:

| The catalogue entry declares | What the portal does with it |
| -- | -- |
| `docs.source: github-path` and `docs.path` | Walks `<path>/**/*.md` at the channel's ref. `wiki` and `readme` are the other two sources. |
| `docs.rootPage` | The page served at `/docs/<id>/`. In this playbook's entry it is `Home.md`. |
| `docs.exclude` | Base file names never published. This playbook excludes `_Sidebar.md`. |
| `docs.channels` | A second published view of the same tree, normally a prerelease channel pinned to a tag. |
| `docs.redirects` | Old addresses that keep working after a page moves into a topic folder. |

Page order comes from `docs.order`, then from the `_Sidebar.md` link order, then from a default. That is
why `_Sidebar.md` is a published contract rather than a local convenience.

Two consequences worth acting on:

- **The catalogue entry, not the repository, decides what is published.** A project joins the portal by
  an entry in that catalogue — there is no file in the repository to add. Its `path`, `rootPage` and
  `exclude` are the published surface, so a page the entry cannot reach exists only in a clone.
- **Write Markdown that survives being rendered as Markdown.** The portal converts or strips everything
  that is not plain Markdown: HTML attribute lists, template syntax and include directives are removed.
  Anything that works only because a renderer extension happens to be installed cannot carry meaning.

## How to write a page

Every content page uses the same four sections, in this order:

| Section | Answers |
| -- | -- |
| **The rule** | What is expected. |
| **Why** | The failure the rule prevents. |
| **How it is enforced** | The mechanism that makes it true — or an explicit statement that it is a review obligation. |
| **At your scale** | What to do when the repository is smaller or larger than the one the rule came from. |

`_Sidebar.md` is navigation rather than a content page. `Home.md` is the hub rather than a rule, and
`Engineering-Principles.md` is an index: it states each of the eleven universal rules in one line, plus
the conditional persistence rules, and links to the page that states it in full, so a rule is never
written twice.

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
and it is the fastest way for a reader to decide whether the rule applies to them. Then check the name is
still the current one: a renamed repository keeps resolving through a redirect, so a stale citation looks
healthy until it is compared with the organisation. [Cross-Repository
Harvest](../governance/Cross-Repository-Harvest.md) is that comparison, and the snapshot it records.


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
| The wiki is plain Markdown in `docs/wiki`, with no site configuration beside it | Review |
| Every content page, including pages in topic folders, is in `_Sidebar.md` and the `Home.md` table | `DocumentationTests` |
| The sidebar sections and the `Home.md` headings carry the same labels, in the same order | `DocumentationTests` |
| Every relative link in every page resolves | `DocumentationTests` |
| Pages follow the four-section shape | Review |
| A page sits in the folder for its section | Review |
| Fences declare a language | `DocumentationTests` |
| `README.md` and `AGENTS.md` point in rather than duplicate | Review |

The navigation, the links and the fences are the conventions a machine can check, and they are the ones
that break silently: a page nobody can reach is a page that does not exist, a link into the wrong folder
is a 404 nobody sees in review, and a bare fence renders as an untyped block. `DocumentationTests` in
`src/tests/Repository.UnitTests` walks every page in every folder for all three. The rest is a review
obligation, and this page says so rather than implying otherwise.

## At your scale

- **One page.** `docs/wiki/Home.md` and `_Sidebar.md`. Two small files.
  No folders, and the sidebar is a flat list.
- **A library.** One page per public concern, plus a getting-started page that is the shortest path to a
  working example. Add a folder once a topic has two or three pages. Keep the consumer contract on its own
  page — it is the page that must not drift.
- **A platform.** One folder per subsystem, and a single hub that links them all, grouped by the same
  sections the sidebar uses. The hub is what stops a wiki becoming a search problem.
- **Several repositories.** Keep each wiki with its code, and let one site aggregate them. The aggregation
  is a build concern; ownership stays with the repository.

## See also

- [Home](../Home.md)
- [Agent Enablement](../agents/Agent-Enablement.md)
- [Contributing](../governance/Contributing.md)
