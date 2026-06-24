Generate a paste-ready pull-request description for the current branch's changes.

$ARGUMENTS

If $ARGUMENTS is empty, describe all changes on the current branch vs `main`. If it names a base ref or a scope, use that instead.

---

## Steps

1. Gather the actual changes — do not guess:
   - `git fetch origin main`
   - `git log --oneline origin/main..HEAD` and `git diff --stat origin/main...HEAD`
   - Read the real diff for anything non-obvious (don't describe what you didn't read).
2. Produce the PR description in this exact structure. Keep it tight — it becomes the squash-merge commit body:

   ### Summary
   - Up to **5 bullets**, imperative mood: what changed and why. No filler, no restating the diff line-by-line.

   ### Risks & rollback
   - The realistic failure modes this change introduces (or "none — config/docs only").
   - Rollback: how to revert safely (revert the squash commit; restore which config/setting).

   ### Reviewer focus
   - 2–4 specific things a reviewer should scrutinise (the riskiest hunks, public-API surface, behaviour changes).
   - If the diff touches auth, secrets, or HTTP headers, say so explicitly so the `security-reviewer` agent is run; otherwise the `code-reviewer` agent covers it.

3. Output the description as one copy-paste block.

## Constraints

- PRs are **squash-merged** — write the description as the squash commit body would read.
- If the change touches the published `IntegratoR.*` surface (signatures, visibility, serialised output), flag it under Reviewer focus and point at the breaking-change rules in `.claude/rules/api-compatibility.md` / the `csharp-api-design` skill. Do not restate those rules.
- End the PR body with the required footer line: `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Do **not** create the PR, push, commit, or tag. This command only drafts the description.
