Generate or update the IntegratoR GitHub wiki.

$ARGUMENTS

If `$ARGUMENTS` is empty, produce a documentation **plan** for the whole wiki (the page list mapped to the three groups, each with its page type, plus any drift to fix).
If `$ARGUMENTS` names a component, layer, or type (e.g. "OData", "CreateCommand", "pipeline behaviours"), generate or update the page(s) for that area.

## This command is governed by the `wiki-documentation` skill

The skill owns every house standard — do **not** restate them here:

- Code-first, one-Diataxis-mode-per-page philosophy; lean British-spelling voice + banned words (`references/voice-and-exclusions.md`).
- The three-group information architecture (Get Started / Use Cases / Reference).
- Page-type skeletons for each mode (`references/page-templates.md`).
- Rationed GitHub alert callouts, `## See Also`, `> Last verified against vX.Y.Z` freshness stamps, inline `(since vX.Y)` version tags.
- The `/wiki` → `.wiki.git` deploy recipe.

Follow the skill. It loads automatically for wiki work.

## Ground truth before writing

Read the entity/handler source and honour every `[ODataField(IgnoreOnCreate/IgnoreOnUpdate)]` and `[JsonPropertyName]`; read the tests for realistic examples; consult `.claude/plans/wiki-ground-truth.md`. Verify every claim and code block against source, a test, or a dated live run — **never** against another wiki page. Use the current non-generic `BaseEntity`, never the `[Obsolete]` `BaseEntity<TKey>`. The only public DI entry point is `AddIntegratoR`.

## Output target

- Pages are Markdown files in the in-repo `/wiki` folder; deploy syncs them to `<repo>.wiki.git`.
- Filenames are **Title-Case-with-hyphens** so the slug equals the title — `Send-Commands.md` → "Send Commands".
- Internal links use Markdown slug syntax `[Send Commands](Send-Commands)` — **never** `[[Page-Name]]`, never hard-coded wiki URLs.
- `Home.md` is the routing hub; `_Sidebar.md` is the nav; `_Footer.md` holds persistent links.

## Steps

1. **Empty `$ARGUMENTS`** — scan the solution (`dotnet sln list` / `.csproj`), read the public surface, and output the plan: pages per group, page type each, and drift to fix.
2. **Named area** — find the source, read it (public API, parameters, error paths) and the tests, then write/update the page(s) using the skill's skeleton for that page type: a runnable example, the failure path, a `## See Also`, and a freshness stamp.
3. **After generating** — run the skill's pre-publish checklist (grep for `[[`, `BaseEntity<`, and banned words; confirm every page ends with `## See Also`, carries a stamp, and is reachable from `_Sidebar.md`).
