# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are computed automatically by [GitVersion](https://gitversion.net/) in ContinuousDelivery mode.

> **This is the `support/1.3` hotfix line.** It branches from **1.3.5** and does **not** contain 1.3.6
> or anything later — despite the higher patch number, 1.3.7 has neither the RELion removal nor the
> consumer generic-handler fix that shipped in 1.3.6. Consumers who can take the breaking changes
> should move to the main line instead.

## [1.3.7] - Unreleased

### Fixed

- **Batch writes no longer throw before reaching the network.** `AddBatch` (and the update/delete batch
  paths) failed with `InvalidOperationException: This operation is not supported for a relative URI`.
  PanoramicData's changeset serialises every sub-request through `HttpMessageContent`, which reads
  `Uri.PathAndQuery` on a relative sub-request URI — and that throws for any relative `Uri`. The
  failure happened while buffering the request body, so **no batch operation ever reached D365**.
  - Affected **every published version** from 1.1.0 to 2.0.1: the batch code was identical throughout,
    and the defect is present in PanoramicData 10.0.55 and 10.0.84 alike, so neither upgrading nor
    pinning the library avoided it.
  - The three batch methods now emit the `multipart/mixed` OData `$batch` body directly (OASIS OData
    v4.01 Part 1 §11.7.7) and send it through the named `"ODataClient"`, which carries the same
    authentication, Polly resilience, and base address as the rest of the traffic. The request line is
    built as text, so `Uri` is never involved; a relative URL in a sub-request line is what the
    specification calls for anyway.
  - Semantics are unchanged: one atomic changeset, all-or-nothing. When it rolls back, every operation
    is reported as failed, because nothing was applied.

### Security

- **Key values are rejected when they contain control characters.** The batch body is assembled as
  text, so a composite-key or scalar key value carrying CR or LF ended up in the embedded
  `METHOD url HTTP/1.1` request line and could terminate it early, injecting headers — or a forged
  request — into that part. The read path never had this exposure: its literals pass through
  `System.Uri`, which rejects a stray CR or LF. Moving writes off PanoramicData is what removed that
  safety net, so the guard ships with the same change. Entity payloads were never affected
  (`System.Text.Json` escapes control characters, and batch boundaries are per-request GUIDs).

### Changed

- `ODataClientAdapter` gained a second constructor taking `IHttpClientFactory`, which DI now uses.
  Batch writes require it; the single-argument constructor throws a message naming the one to use.
  This is additive — the existing constructor is unchanged, and batch never worked through it either.
- **An empty batch no longer issues an HTTP request.** 1.3.5 sent an empty `$batch` to D365 when
  handed zero entities; the batch methods now return an empty result without going to the wire.
  Callers see no difference — `AddBatchAsync` and its siblings already reported success for an empty
  result — but the network round-trip, and whatever D365 made of an empty changeset, are gone.

## [Unreleased]

### Added
- Open source best practices: community health files, CI/CD, build infrastructure
- Central package version management via `Directory.Packages.props`
- Shared build properties via `Directory.Build.props`
- `.editorconfig` for machine-enforceable code style
- GitHub Actions CI/CD pipeline
- Issue and PR templates
- Dependabot configuration for automated dependency updates
- Branch protection rules on `main`

## [0.1.0] - 2025

### Added
- Clean Architecture project structure with inward-pointing dependencies
- CQRS pattern implementation with MediatR
- FluentResults integration replacing exceptions for flow control
- Generic OData client with authentication and resilience (Polly)
- D365 Finance & Operations entity models and CQRS handlers
- RELion OData integration
- Azure Durable Functions orchestrators with fan-out/fan-in patterns
- Pipeline behaviours: logging, validation (FluentValidation), caching
- Result serialization for Durable Functions replay (`ResultJsonConverter`)
- Composite key support for multi-field D365 entity keys
- Redis distributed caching support
- MSAL client credentials authentication
- OData2Poco code generation integration
- GitVersion configuration for automatic versioning
