# Contributing

## The rule

This repository is documentation with a small reference implementation attached. The documentation is the
product; the reference implementation exists to prove the documentation is not guesswork.

### What belongs here

A rule belongs in the playbook when all four of these are true:

1. **It came from a real repository and a real failure.** "This would be nicer" is not enough. The page should
   be able to say which failure the rule prevents.
2. **It is reusable at more than one size.** A rule that only makes sense in a repository with eleven
   packages needs an "at your scale" section explaining what the smaller version is.
3. **It is not the SDK's to state.** Where the SDK already documents its own behaviour, link to it. A second
   statement of the same fact is a statement that will drift.
4. **It is enforced, or it is labelled as a review obligation.** Both are acceptable. Blurring them is not.

What does not belong: anything that names a product, a customer, or a repository that is not public. The
playbook is a published artefact, and a rule that can only be justified by something private cannot be
reviewed by the people it is published to.

## Why

The four criteria are the whole value of the playbook. A rule without a failure behind it is an opinion, and
opinions accumulate until the useful rules are indistinguishable from the noise. A rule that only works at
one size gets cargo-culted at another. A rule that restates the SDK's own documentation is a second source of
truth that will drift. And a rule that claims enforcement it does not have is worse than no rule at all,
because people rely on the guarantee and stop looking. Each criterion removes one of those failure modes.

## Adding a page

1. **Write the page** in `docs/wiki/<Page-Name>.md`. Title case, hyphens between words.
2. **Follow the four sections** from [Documentation Convention](Documentation-Convention.md): the rule, why,
   how it is enforced, at your scale.
3. **Add it to `_Sidebar.md`.** A page that is not in the navigation is a page nobody finds.
4. **Add it to the table in `Home.md`.** The hub is how a reader discovers a page they did not know to look
   for.
5. **Link both ways.** A page with no inbound link is a page that will be deleted by accident; a page with no
   outbound links is a dead end.
6. **Link to the authority, and name the evidence.** Where a rule is the SDK's, link to the SDK's page. Where
   the evidence is a repository, name it, so a reader can check the claim against running code.

## Changing a rule

Changing a rule is a bigger change than adding one, because pages link to it and code may depend on it.

- **Say what changed and why** in the pull request description, including the failure that motivated it.
- **Update every page that states the rule**, not just the one you started from. Cross-references are the
  mechanism that keeps a multi-page playbook coherent.
- **Check the reference implementation still demonstrates it.** If the rule changed and `src/` did not, either
  the rule is not demonstrated or the demonstration is now wrong.
- **Check the "How it is enforced" table.** A rule that moved from review to a build check — or the reverse —
  is a change to the promise the page is making.

## The reference implementation

`src/` follows the conventions it documents, and that is the point. Two consequences:

- **A change to `src/` that breaks a documented convention is a bug**, even if the build passes. The
  conventions include the ones only review can check.
- **A new mechanism documented in the wiki should be demonstrated in `src/` where it is feasible.** Where it
  is not — a published package contract, a persistence layer, a second CI tier — say so on the page and point
  at the public repository that does demonstrate it.

## How it is enforced

Three mechanisms, and they are the ones a change to this repository actually passes through.

### Commit messages

Conventional Commits, enforced by a `commit-msg` hook. The allowed types are in `commitlint.config.mts`:

```text
build  chore  ci  docs  feat  fix  perf  refactor  revert  style  test
```

The header is limited to 100 characters, the subject must not be sentence case, and body lines wrap at 100.
A documentation-only change is `docs:`; a change to the reference implementation is `feat:` or `fix:`; a
change to the pipeline configuration is `ci:` or `build:`.

### Validation before handing work back

```text
just lint-check     # CSharpier check
just build          # dotnet build (Debug)
just test           # dotnet test
just pipeline-pr    # restore, build, lint, test through the shared Purview.Build tool
```

Then state exactly what ran. If a check was skipped or blocked, give the concrete reason and the remaining
risk — an unstated gap is the one that gets discovered in production.

For a documentation-only change, the checks that matter are the ones that catch a broken reference: the
navigation in `_Sidebar.md`, the table in `Home.md`, and any relative link you added.

### Review expectations

A reviewer should be able to answer these from the pull request alone:

- Which failure does this rule prevent, and where has it happened?
- Is the rule enforced by a build, or is it a review obligation? Does the page say which?
- What does a repository smaller than the one this came from do instead?
- If the rule is the SDK's, why is it restated here rather than linked?
- Does `src/` still demonstrate the conventions, or has it drifted?

## At your scale

This page is for changing the playbook itself. A repository that is only *borrowing* from the playbook does
not need any of it: take the pages and templates that fit and leave the rest, as [Adopting This at Your
Scale](Adopting-This-At-Your-Scale.md) describes. The smaller version of this process, for a
documentation-only change, is: edit the page, keep `_Sidebar.md` and `Home.md` in step, and run the reference
checks in [Validation before handing work back](#validation-before-handing-work-back).

## See also

- [Documentation Convention](Documentation-Convention.md)
- [Engineering Principles](Engineering-Principles.md)
- [Adopting This at Your Scale](Adopting-This-At-Your-Scale.md)
- [Release Flow](Release-Flow.md)
