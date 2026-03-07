Create a pull request for the current branch targeting main.

$ARGUMENTS

## Steps

1. Run in parallel to understand the branch:
   - `git status`
   - `git log main..HEAD --oneline`
   - `git diff main...HEAD --stat`
   - Check if branch is pushed: `git rev-parse --abbrev-ref @{upstream}`

2. If not pushed, run `git push -u origin HEAD`.

3. Analyse ALL commits on the branch and draft:
   - **Title**: Under 70 chars, imperative mood.
   - **Body**: Summary + Test Plan sections.

4. Create the PR:

```bash
gh pr create --title "<title>" --body "$(cat <<'EOF'
## Summary
<1-3 bullet points>

## Test Plan
- [ ] <verification steps>

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

5. Return the PR URL.
