---
codename: "test-suite"
title: "IntegratoR Test Suite"
status: active
priority: "high"
depends_on: []
technologies:
  - "xUnit.v3 3.0.x"
  - "NSubstitute 5.3.x"
  - "FluentAssertions 8.x"
  - "Microsoft.NET.Test.Sdk 18.x"
  - "C# (.NET 10)"
created: "2026-02-18"
updated: "2026-02-18"
---

## Goal

Establish comprehensive test coverage for the IntegratoR framework across all 5 library projects (excluding SampleFunction). Create a shared TestKit with test doubles, builders, and custom assertions, then systematically test every layer from domain primitives through infrastructure to integration. Target ~299 tests across 48+ test classes, providing a reliable safety net for the CQRS pipeline, Result pattern, OData integration, resilience policies, and JSON serialization.

## Must Do

- Follow Clean Architecture test structure: 1:1 test project per source project + shared TestKit
- Use xUnit.v3, NSubstitute, FluentAssertions as the test framework stack
- Mirror source project folder/namespace structure in test projects
- Follow AAA pattern, British spelling (Behaviour), and existing naming conventions
- Add test package versions to Directory.Packages.props
- Add InternalsVisibleTo for OData.Tests in OData.csproj
- Execute missions in dependency order (TestKit first, then Abstractions through RELion)
- Use TestKit test entities for generic handler tests, not production D365 entities

## Must Not Do

- Do not test IntegratoR.SampleFunction (Azure Functions host)
- Do not modify production code unless strictly required (e.g., InternalsVisibleTo)
- Do not use production D365 entities in generic handler tests
- Do not mock the system under test

## Ideas

- Custom FluentAssertions extensions for Result<T> pattern (BeSuccessful, BeFailed, HaveErrorCode)
- FakeHttpMessageHandler for testing HTTP pipeline without real network calls
- FakeCacheService for CachingBehaviour tests
- TestEntityBuilder with fluent API for test data construction
