# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are computed automatically by [GitVersion](https://gitversion.net/) in ContinuousDelivery mode.

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
