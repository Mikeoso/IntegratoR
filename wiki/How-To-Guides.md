# How-To Guides

Practical, step-by-step recipes for common IntegratoR tasks. Each guide shows working code with realistic D365 F&O examples.

## Entity Operations

- **[[Create-an-Entity]]** — Create a new entity via MediatR command or direct service call
- **[[Update-an-Entity]]** — Update an existing entity with composite key resolution
- **[[Delete-an-Entity]]** — Delete an entity and handle D365 idempotent deletes
- **[[Batch-Multiple-Operations]]** — Create, update, or delete entities in atomic batch operations
- **[[Query-Entities-by-Key]]** — Retrieve a single entity using its composite key
- **[[Query-Entities-by-Filter]]** — Query multiple entities with LINQ filter expressions

## Cross-Cutting Concerns

- **[[Handle-Errors-with-Result]]** — Use FluentResults and IntegrationError for error handling
- **[[Add-Validation-to-a-Command]]** — Add FluentValidation rules that auto-run in the pipeline
- **[[Cache-Query-Results]]** — Cache query responses with ICacheableQuery
- **[[Configure-Retry-and-Circuit-Breaker]]** — Configure Polly resilience policies for OData calls

## Extensibility

- **[[Define-a-Custom-Entity]]** — Create a new D365 F&O entity with attributes and composite keys
- **[[Write-a-Specialized-Command]]** — Create domain-specific commands wrapping generic operations

## D365 F&O Specific

- **[[Create-a-Ledger-Journal]]** — Create journal headers and lines end-to-end
- **[[Build-Financial-Dimension-Strings]]** — Parse and build dimension strings for D365 F&O
- **[[Query-Dimension-Formats]]** — Retrieve dimension formats with automatic caching

## RELion Integration

- **[[Configure-the-RELion-Connection]]** — Set up the RELion API connection and authentication
- **[[Query-RELion-Data]]** — Query ledger account mappings and journal lines from RELion

## Azure Functions Host

- **[[Set-Up-an-Azure-Functions-Host]]** — Wire up IntegratoR in an Azure Functions composition root
- **[[Build-a-Durable-Functions-Orchestration]]** — Fan-out/fan-in with Result pattern and MediatR

## Testing

- **[[Test-with-the-TestKit]]** — Test commands, queries, and handlers with TestKit fakes
