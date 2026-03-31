# Skill and Agent Routing Guide

This project has specialised skills and agents. Using the right one avoids duplication and ensures quality. **Consult this guide before starting work.**

## Decision Flow

**Before planning or implementing**, ask: does this task match a skill or agent below?

### Planning & Design → Plan mode with interview
**When:** Non-trivial features, restructuring, design decisions, anything touching architecture.
**How:** Enter plan mode. Interview the user with focused questions and options before proposing an approach. Get approval before implementing.
**Not for:** Bug fixes, single-file changes, documentation-only changes.

### Test Planning & Strategy → `test-planning` skill (AUTO-TRIGGERS before test writing)
**When:** ANY request involving tests — "write tests", "add tests", "test this", planning test strategy, assessing change impact, reviewing test quality.
**How:** The skill plans what to test, then hands a brief to the `test-writer` agent. Three modes: new feature coverage, change impact analysis, quality review.
**Flow:** User asks for tests → `test-planning` skill plans → brief → `test-writer` agent writes code.

### Writing Tests → `test-writer` agent (receives brief from test-planning)
**When:** The `test-planning` skill has produced a brief and it's time to write test code.
**How:** Launch via Agent tool with the brief. The agent reads existing test patterns, follows xUnit v3 conventions, and uses TestKit infrastructure.
**Not for:** Trivial structural tests (DI registration, config binding, POCO properties).

### Library Documentation → `context7-docs` skill
**When:** Need to verify API signatures, method parameters, or code examples for third-party libraries (Polly, MediatR, FluentValidation, FluentResults, PanoramicData.OData.Client, xUnit, NSubstitute).
**Not for:** Microsoft/.NET documentation (use `microsoft-docs`), architecture decisions.

### Microsoft Documentation → `microsoft-docs` skill
**When:** Need Microsoft Learn documentation — .NET/Azure architecture, quickstarts, configuration guides, service limits.
**Not for:** Code samples (use `microsoft-code-reference`), non-Microsoft technologies.

### Microsoft Code Samples → `microsoft-code-reference` skill
**When:** Need to verify SDK method signatures, find working code examples, catch hallucinated methods, or look up Azure SDK APIs.
**Not for:** Conceptual documentation (use `microsoft-docs`).

### Wiki Documentation → `/docs` or `/documentation` command
**When:** Need to generate or update IntegratoR wiki pages.
**How:** Invoke via slash command.

### Memory Consolidation → `/dream` skill
**When:** End of a session or when memories feel stale. Synthesises learnings into durable memory files.

## Common Overlaps — How to Choose

| Situation | Use |
|---|---|
| "Plan the new query handler" | Plan mode with interview |
| "Restructure the RELion settings" | Plan mode with interview |
| "What tests do we need for the batch handler?" | `test-planning` skill → `test-writer` agent |
| "Write tests for ODataService" | `test-planning` skill (auto-triggers) → `test-writer` agent |
| "I changed the auth handler, which tests break?" | `test-planning` skill (change impact mode) |
| "Review our handler tests for bloat" | `test-planning` skill (quality review mode) |
| "How does Polly CircuitBreakerAsync work?" | `context7-docs` skill |
| "What are Azure Functions timeout limits?" | `microsoft-docs` skill |
| "Verify IOptions<T> binding syntax" | `microsoft-code-reference` skill |
| "Fix NullRef in the query handler" | None — just fix the bug |
| "Rename a method across the codebase" | None — just do the refactor |
