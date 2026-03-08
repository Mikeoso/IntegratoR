# Install the Framework

IntegratoR targets **.NET 10** with `LangVersion=preview` and nullable reference types enabled.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- An IDE that supports .NET 10 (Visual Studio 2022, Rider, VS Code with C# Dev Kit)

## Install the Packages

```bash
dotnet add package IntegratoR.Abstractions
dotnet add package IntegratoR.Application
dotnet add package IntegratoR.OData
dotnet add package IntegratoR.OData.FO
dotnet add package IntegratoR.RELion
```

`IntegratoR.Abstractions` and `IntegratoR.Application` are required. Add `IntegratoR.OData.FO` for D365 F&O integration, or `IntegratoR.RELion` for RELion API integration.

## Core Dependencies

These are pulled in transitively -- you do not need to install them separately:

| Package | Version | Purpose |
|---------|---------|---------|
| FluentResults | 4.0.0 | `Result<T>` pattern for error handling |
| MediatR | 12.5.0 | CQRS pipeline (commands, queries, behaviours) |
| FluentValidation | 12.1.1 | Request validation in the pipeline |

## Set Up the Project File

IntegratoR uses **Central Package Management**. All versions live in `Directory.Packages.props` at the solution root. Your project files reference packages without version attributes:

```xml
<ItemGroup>
  <PackageReference Include="IntegratoR.Abstractions" />
  <PackageReference Include="IntegratoR.Application" />
  <PackageReference Include="IntegratoR.OData" />
  <PackageReference Include="IntegratoR.OData.FO" />
</ItemGroup>
```

## Verify the Installation

```bash
dotnet build
```

A successful build confirms that all packages resolved correctly.

## What Just Happened

- You installed the IntegratoR framework packages into your project.
- Core dependencies (FluentResults, MediatR, FluentValidation) were pulled in transitively.
- Central Package Management ensures consistent versions across all projects in your solution.

## See Also

- [[Configure-the-OData-Connection]] — configure settings after installing packages
- [[Register-Services-in-Your-Host]] — register services in your DI container
- [[Create-Your-First-Entity]] — define your first entity class
