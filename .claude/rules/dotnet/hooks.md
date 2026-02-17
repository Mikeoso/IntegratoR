---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Claude Code Hooks for .NET

## Recommended Hook: Format on Save

Add the following to `.claude/settings.json` to auto-format C# files after every Write or Edit:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "command": "dotnet format --include \"$FILE\" --no-restore --verbosity quiet",
        "timeout": 15000
      }
    ]
  }
}
```

This runs `dotnet format` on the specific file that was just written or edited. The `--no-restore` flag skips NuGet restore for speed, and `--verbosity quiet` suppresses noise.

**Note:** There is no `.editorconfig` in this project yet. `dotnet format` will apply the default .NET formatting rules until one is added.

## Why `dotnet build` Is Not a Hook

Do **not** add `dotnet build` as a PostToolUse hook. A full solution build takes too long (5-15+ seconds) and would block after every file edit. Instead:
- Run `dotnet build` manually when you need to verify compilation
- Use `dotnet build --no-restore` when packages haven't changed

## Future: Analyzer Packages

When the project adds an `.editorconfig` and code analyzers, consider adding these packages to `Directory.Build.props`:

- `Microsoft.CodeAnalysis.NetAnalyzers` — built-in .NET code quality rules
- `StyleCop.Analyzers` — style consistency enforcement
- `Meziantou.Analyzer` — additional best-practice rules

These will integrate with `dotnet format` to enforce conventions automatically.
