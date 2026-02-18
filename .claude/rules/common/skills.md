# Skill Orchestration

Located in `~/.claude/skills/`:

| Skill | Purpose | When to Use |
|-------|---------|-------------|
| microsoft-docs | Official Microsoft Documentation | Research of offical microsoft documentation |
| microsoft-code-reference | Look up Microsoft API references | Find working code samples, and verify SDK code is correct. Use when working with Azure SDKs, .NET libraries, or Microsoft APIs |

## Immediate Skill Usage
1. Verifying Documentation (Setup, Limitations, Technologies) - Use **microsoft-docs** Skill
2. Verifying SDK Usage, Best Practices and Snippets - Use **microsoft-code-reference**

## Knowledge Gathering

ALWAYS use skills as an additional verification point.

**Do not** use skills as only validation point, always respect project context

```markdown
# GOOD
1. Verify existing project pattern and implementations
2. Invoke specified Skill for additional knowledge and verification
3. Plan implementation
4. Implement in parallel
```

```markdown
# BAD
1. Plan
2. Implement
```

## Multi-Perspective Analysis

For complex problems, use split role sub-agents:
- Facutal reviewer
- Senior engineer
- Security expert
- Consistency reviewer
- Redudancy cheker