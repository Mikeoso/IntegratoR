# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are computed automatically by [GitVersion](https://gitversion.net/) in ContinuousDelivery mode.

## [Unreleased]

> **Next release: 2.0.0 (MAJOR).** The architecture-review fix series (PRs #131–135) shipped breaking public-API changes (see **Changed — Breaking**) that were mis-tagged `+semver: minor`; `GitVersion.yml` pins `next-version: 2.0.0` to correct the signal.

### Added
- Composite-key **write** support (Update / Delete / batch) for D365 F&O entities via a raw-`HttpClient` bypass in `ODataClientAdapter`, closing the long-standing PanoramicData composite-key write limitation.
- Strongly-typed `$orderby` on queries (`IODataService` / `IODataClientAdapter` typed overloads; `[JsonPropertyName]`-aware translation).
- `ODataSettingsValidator` (`IValidateOptions<ODataSettings>` + `ValidateOnStart`): fails fast on an auth header smuggled into `DefaultHeaders`, on per-mode credential gaps, and on an unrecognised `AuthenticationMode`.
- `IBatchService<TEntity>` (in `IntegratoR.Abstractions`) with generic `Create`/`Update`/`DeleteBatchCommandHandler<T>`.
- `ResultFactory` — cached-reflection failed-result factory shared by `ValidationBehaviour` and `ODataExceptionHandler`.
- Non-generic `BaseEntity` base class.
- Open source best practices: community health files, CI/CD, build infrastructure
- Central package version management via `Directory.Packages.props`
- Shared build properties via `Directory.Build.props`
- `.editorconfig` for machine-enforceable code style
- GitHub Actions CI/CD pipeline
- Issue and PR templates
- Dependabot configuration for automated dependency updates
- Branch protection rules on `main`

### Changed — Breaking
- `IODataClientAdapter.FindEntriesAsync` gained an `orderBy` parameter (ordered `(keySelector, descending)` tuples) inserted before `skip`/`top`. Implementors and named-argument callers must update.
- `CreateBatchCommand<T>` / `UpdateBatchCommand<T>` / `DeleteBatchCommand<T>` (and the F&O command records deriving from them) now take `IReadOnlyList<T>` instead of `IEnumerable<T>`, removing multiple-enumeration and giving O(1) `Count`.
- `GetDimensionOrdersQuery` positional parameters renamed `dimensionFormat`/`hierarchyType` → `DimensionFormat`/`HierarchyType` (C# PascalCase convention).

### Deprecated
- `BaseEntity<TKey>` (the `TKey` parameter was never used — derive from the non-generic `BaseEntity`), `IODataService.FindAll` (use `FindAllAsync`), `ODataBatchException`, `ICacheableQuery.GenerateCacheKey` / `GetCacheKeyValues`, and `ODataMetadataProvider`. All marked `[Obsolete]`; removed in the next MAJOR.

### Security
- 401/403 responses and OAuth token-acquisition failures no longer leak MSAL / tenant detail in HTTP `ReasonPhrase` or error messages (full detail is logged server-side only).
- `LoggingBehaviour` moved request-body destructuring from Information to Debug to avoid logging payloads at the default level.

### Fixed
- `LoggingBehaviour` logged a failed `Result<T>` as a success (only the non-generic `Result` was inspected).
- Smoke-test Update/Delete verify steps no longer misfire when the preceding write fails.

### Removed
- `IntegratoR.RELion` integration module (RELion property-management REST API client, auth handler, domain entities, and ledger query). Out of scope for the framework's Microsoft-business-application OData focus; the module was never published to NuGet.

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
