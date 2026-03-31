# OData Conventions

## Settings Structure

- `ODataSettings` uses nested sub-objects: `Authentication` and `Resilience`.
- `AuthenticationMode` enum: `ApiKey` (via APIM) or `OAuth` (direct client credentials).
- Access OAuth credentials via `settings.Authentication.OAuth.ClientId` etc.
- Access resilience via `settings.Resilience.RetryCount` etc.
- `DefaultHeaders` are under `ApiManagement` — only sent in ApiKey mode.
- JSON config keys use `:` separator locally, `__` (double underscore) on Azure/Linux.

## Entity Patterns

- Entities inherit from `BaseEntity<TKey>` and must implement `GetCompositeKey()`.
- D365 uses composite keys: typically `DataAreaId` + business key.
- `[Table("EntitySetName")]` maps to OData entity sets.
- `[ODataField(IgnoreOnCreate = true)]` for server-generated fields (e.g., `JournalBatchNumber`, `LineNumber`).
- `[ODataField(IgnoreOnUpdate = true)]` for immutable fields.
- Before writing code examples with D365 entities, read the entity source to check which fields have `IgnoreOnCreate`/`IgnoreOnUpdate`.

## Service Layer

- `ODataService<T>` implements `IService<T>` — do NOT wrap it in a repository.
- `IODataBatchService<T>` for batch operations.
- Use `IMediator` to send commands/queries — never call services directly from endpoints.

## Configuration Binding

- DI binding: `services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"))`.
- Programmatic: `services.AddODataClient(options => { options.Url = "..."; })`.
- `PostConfigure` composes via `IntegratoRBuilder.ConfigureOData(Action<ODataSettings>)`.
- All nested objects initialised with `= new()` — safe for `PostConfigure` lambdas.
