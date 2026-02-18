# IntegratoR Test Suite -- Implementation Plan

> **Date:** 2026-02-18
> **Status:** Approved, ready for implementation
> **Scope:** All library projects except `IntegratoR.SampleFunction`

---

## Context

The IntegratoR framework currently has **zero test coverage** across 5 library projects. This plan adds a comprehensive test suite covering every testable class, method, and code path. The goal is to establish a reliable safety net for the framework's CQRS pipeline, Result pattern, OData integration, resilience policies, and JSON serialization -- all critical for production D365/RELion data flows.

---

## Test Architecture

### Frameworks

| Package | Version | Purpose |
|---|---|---|
| xunit.v3 | 3.0.x | Test framework (.NET 10 compatible, parallel execution, `[Theory]`/`[InlineData]`) |
| NSubstitute | 5.3.x | Mocking (clean syntax for FluentResults return types, no `.Object` ceremony) |
| FluentAssertions | 8.x | Assertions (completes Fluent trilogy with FluentResults + FluentValidation) |
| Microsoft.NET.Test.Sdk | 18.x | Test host |

### Project Structure (1:1 mapping + shared TestKit)

```
tests/
  IntegratoR.TestKit/              -- Shared test doubles, builders, custom assertions
  IntegratoR.Abstractions.Tests/   -- Quest 1: Domain primitives, Result types, serialization
  IntegratoR.Application.Tests/    -- Quest 2: Pipeline behaviours, handlers, validators, services
  IntegratoR.OData.Tests/          -- Quest 3: OData client, auth, exception handling, resilience
  IntegratoR.OData.FO.Tests/       -- Quest 4: D365 F&O entities, dimensions, handlers
  IntegratoR.RELion.Tests/         -- Quest 5: RELion service, auth, DTOs, queries
```

Each test project mirrors the source project's folder/namespace structure.

### Conventions

- **AAA pattern** (Arrange-Act-Assert) with clear separation in every test
- **British spelling** throughout: `Behaviour`, not `Behavior`
- **Test class naming**: `{ClassName}Tests`
- **Test method naming**: `MethodName_Scenario_ExpectedResult`
- Mock only **direct dependencies** -- never mock the system under test
- Test entities from `IntegratoR.TestKit` for generic handler tests (not production D365 entities)
- `InternalsVisibleTo("IntegratoR.OData.Tests")` in `IntegratoR.OData.csproj` for internal types

---

## Quest Summary

| Quest | Project | Missions | Approx Tests | Complexity | Doc |
|---|---|---|---|---|---|
| 0 | TestKit (shared infra) | 4 | N/A (infrastructure) | M | [quest-0-testkit.md](quest-0-testkit.md) |
| 1 | Abstractions | 8 | ~51 | Mostly S | [quest-1-abstractions.md](quest-1-abstractions.md) |
| 2 | Application | 13+ | ~92 | Mix S/M | [quest-2-application.md](quest-2-application.md) |
| 3 | OData | 5 | ~59 | M/L | [quest-3-odata.md](quest-3-odata.md) |
| 4 | OData.FO | 12 | ~56 | Mostly S | [quest-4-odata-fo.md](quest-4-odata-fo.md) |
| 5 | RELion | 6 | ~41 | S/M/L | [quest-5-relion.md](quest-5-relion.md) |
| **Total** | | **48+** | **~299** | | |

---

## Implementation Order

Execute quests in dependency order:

1. **Quest 0** (TestKit) -- Foundation for all test projects
2. **Quest 1** (Abstractions) -- Innermost layer, validates domain primitives
3. **Quest 2** (Application) -- Pipeline + handlers, uses TestKit entities
4. **Quest 3** (OData) -- Infrastructure, uses TestKit + Abstractions patterns
5. **Quest 4** (OData.FO) -- D365-specific, depends on OData patterns
6. **Quest 5** (RELion) -- Outermost integration, depends on patterns from all prior

Within each quest, implement missions in listed order (lower-numbered missions provide patterns for later ones).

---

## Key Files to Modify (Non-Test)

| File | Change |
|---|---|
| `IntegratoR.sln` | Add 6 test projects to solution |
| `Directory.Packages.props` | Add test package versions (xunit.v3, NSubstitute, FluentAssertions, Microsoft.NET.Test.Sdk) |
| `IntegratoR.OData/IntegratoR.OData.csproj` | Add `InternalsVisibleTo("IntegratoR.OData.Tests")` |

---

## Verification

After each quest:
```bash
dotnet build           # Verify all test projects compile
dotnet test            # Run full test suite
```

Final verification:
```bash
dotnet test --collect:"XPlat Code Coverage"  # Generate coverage report
```
