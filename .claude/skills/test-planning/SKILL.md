---
name: test-planning
description: >
  Use this skill AUTOMATICALLY whenever tests need to be written, planned, or reviewed — including
  when the user asks to "write tests", "add tests", "test this", or any variation that involves
  creating test code. This skill MUST run before the test-writer agent to ensure tests are
  planned properly, avoiding structural bloat and focusing on real behaviour.

  Trigger this skill when: the user asks to write, add, or create tests for any component; a new
  feature was implemented and needs test coverage; code was changed and you need to figure out
  which tests are affected; the user says "what tests do we need", "plan tests", "test strategy",
  "which tests break", "review test quality", "test coverage for", "write tests for", "add tests
  for", or any request involving test creation; after fixing a bug and needing to verify the fix
  is testable; when reviewing test suites for bloat or quality. This skill is the mandatory
  thinking step before any test writing — it produces the brief that the test-writer agent executes.

  Do NOT trigger for: writing implementation code, documentation, architecture planning, or
  looking up API docs (use context7-docs skill).
---

# Test Planning Skill

This skill is the thinking layer for testing. It figures out *what* needs testing, *why*, and *how* — then hands off to the `test-writer` agent to do the actual writing. It never writes test code itself.

## Your Role

You are a test strategist. You analyse code, identify what behaviours matter, plan test cases, assess change impact on existing tests, and catch test suite quality problems. When it's time to write tests, you compose a clear brief for the `test-writer` agent.

## The Core Question

Before anything else — and visibly in your output — ask and answer: **"What logic could break?"**

Start every test plan with this question applied to each component. Write it out explicitly so the user sees the reasoning. If the answer is "nothing — it's declarative config, a POCO, or a DI registration", state that clearly and move on.

Good test targets have real logic: conditional branching, data transformations, error handling paths, state machines, parsing, filtering, pipeline behaviours, MediatR handler orchestration.

Bad test targets are purely structural: property getters/setters, constructor assignments, DI registrations, `ODataFieldAttribute` declarations, config binding.

## Project Test Infrastructure

- **Framework:** xUnit v3, FluentAssertions, NSubstitute
- **TestKit:** `IntegratoR.TestKit` provides shared infrastructure:
  - `FakeCacheService` — in-memory cache for testing pipeline caching behaviour
  - `FakeHttpMessageHandler` — queues HTTP responses for testing auth handlers and OData calls
  - `TestEntityBuilder` — builds test entities with sensible defaults
  - Custom Result assertions: `BeSuccessful()`, `BeFailed()`, `HaveErrorCode()`, `HaveErrorType()`, `HaveValue()`
- **Test naming:** `MethodName_Scenario_ExpectedResult`
- **Test structure:** mirrors source project structure (e.g., `tests/IntegratoR.OData.Tests/Common/Authentication/`)

## Three Modes

### Mode 1: New Feature — "What tests do we need?"

**Step 1: Understand what was built.** Read the implementation files. Identify the public API surface, the internal logic paths, the error handling, and the dependencies.

**Step 2: Categorise behaviours.** For each component, list the testable behaviours:
- **Happy path** — the normal flow works correctly
- **Edge cases** — empty inputs, null values, boundary conditions
- **Error conditions** — what happens when dependencies fail, input is malformed
- **Integration points** — how components interact (MediatR pipeline, HTTP clients, OData service)

**Step 3: Decide unit vs. integration.** Unit test when the behaviour is self-contained and dependencies can be mocked (handlers, validators, extension methods). Integration test when crossing boundaries (full pipeline, config binding, HTTP client with Polly policies).

**Step 4: Check existing patterns.** Read the test project. Reuse existing helpers and fakes from TestKit. Don't duplicate infrastructure.

**Step 5: Compose the test-writer brief.**

```
Write tests for [component]. Here's what to cover:

Unit tests:
- [Behaviour 1]: [what to assert]
- [Behaviour 2]: [what to assert, including edge case]
- [Error case]: [what should happen when X fails]

Patterns to follow:
- Use FakeHttpMessageHandler for HTTP mocking
- Use Result assertions: result.Should().BeSuccessful() / BeFailed()
- Test naming: MethodName_Scenario_ExpectedResult

Do NOT test:
- [Structural thing that looks tempting but has no logic]
```

### Mode 2: Code Change — "Which tests are affected?"

**Step 1: Identify what changed.** Read the diff. Categorise:
- **Behaviour change** — new tests needed, existing tests may need updating
- **Bug fix** — add a regression test that fails without the fix
- **Refactoring** — existing tests should still pass, no new tests needed
- **New error handling** — add tests for the new path

**Step 2: Find affected tests.** Search test projects for tests exercising the changed code.

**Step 3: Assess impact.** For each affected test: still valid, needs updating, now obsolete, or missing coverage.

**Step 4: Plan the regression test.** For bug fixes: design a test that would have caught the bug. Delegate to `test-writer`.

### Mode 3: Test Quality Review — "Is this test suite good?"

**Step 1: Read the tests.** Scan the test project for the area under review.

**Step 2: Apply the quality checklist.**

| Check | What to look for |
|---|---|
| **Structural bloat** | Tests asserting POCO properties exist, DI doesn't throw, constructors assign fields — delete these |
| **Weak assertions** | `Assert.NotNull()` when you could assert a specific value — strengthen these |
| **Missing edge cases** | Happy path only, no null/empty/boundary tests — add these |
| **Missing error paths** | No tests for when things fail — add these |
| **Test isolation** | Tests depending on execution order or shared mutable state — fix these |
| **Wrong assertion methods** | Using `BeSuccess()`/`BeFailure()` instead of `BeSuccessful()`/`BeFailed()` — fix these |

**Step 3: Produce a report.** List findings grouped by severity (Remove, Fix, Add). Delegate work to `test-writer`.

## What NOT to Test — The Exclusion List

- Entity POCOs (properties, constructors) — no logic, nothing can break
- `ODataFieldAttribute` declarations — asserting an attribute was applied tests xUnit, not your code
- DI extension methods (`AddODataClient()`, `AddApplicationServices()`) — registration is structural
- Config binding (`IOptions<T>`) — the framework handles this (but DO test that nested keys bind correctly when restructuring settings)
- `BaseEntity<TKey>.GetCompositeKey()` — only if it contains conditional logic

## Working with the test-writer Agent

The `test-writer` agent is your hands. You think, it writes. The quality of its output depends on the quality of your brief.

**Good brief:** "Write unit tests for `CreateCommandHandler<T>.Handle`. Cover: successful creation returns Result.Ok with entity, validation failure short-circuits and returns Result.Fail with ErrorType.Validation, service layer exception returns Result.Fail with ErrorType.Failure, cancellation token is respected. Use NSubstitute for IService<T>. Follow the pattern in `DeleteCommandHandlerTests.cs`. Use `result.Should().BeSuccessful()` and `result.Should().BeFailed()`."

**Bad brief:** "Write tests for the create command handler."
