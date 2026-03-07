Analyse the current changes and create a commit.

$ARGUMENTS

## Steps

1. Run `git diff --cached` and `git diff` to see all staged and unstaged changes.
2. Run `git log --oneline -5` to see recent commit style.
3. Analyse changes — determine the nature (feature, enhancement, fix, refactor, test, docs).
4. Stage relevant files by name. Never use `git add -A` or `git add .`.
5. Draft a commit message in imperative mood, under 72 chars, focused on "why" not "what".
6. Commit using a HEREDOC:

```bash
git commit -m "$(cat <<'EOF'
<message>

Co-Authored-By: Claude Code <noreply@anthropic.com>
EOF
)"
```

7. Run `git status` to confirm success.
