# OData Conventions

## Settings Structure

- `ODataSettings` uses nested sub-objects: `Authentication` and `Resilience`.
- `AuthenticationMode` enum: `ApiKey` (via APIM) or `OAuth` (direct client credentials).
- Access OAuth credentials via `settings.Authentication.OAuth.ClientId` etc.
- Access resilience via `settings.Resilience.RetryCount` etc.
- `DefaultHeaders` are under `ApiManagement` — only sent in ApiKey mode.
- JSON config keys use `:` separator locally, `__` (double underscore) on Azure/Linux.

## Entity Patterns

- Entities inherit from the non-generic `BaseEntity` and must implement `GetCompositeKey()`. (`BaseEntity<TKey>` is `[Obsolete]` — the `TKey` parameter was never used; removed next MAJOR.)
- D365 uses composite keys: typically `DataAreaId` + business key.
- `[Table("EntitySetName")]` maps to OData entity sets.
- `[ODataField(IgnoreOnCreate = true)]` for server-generated fields (e.g., `JournalBatchNumber`, `LineNumber`).
- `[ODataField(IgnoreOnUpdate = true)]` for fields that are **read-only on update** in D365 — not only immutable business keys but also server-computed / status fields. If ANY such field is present in the update payload, D365 rejects the **whole** PATCH with an `ODataSecurityException` (HTTP 403, `"update not allowed for field 'X'"`), not just that field. Verified against live JFI (2026-07-01): `LedgerJournalHeader` needed `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, `JournalTotalCredit`, and `JournalName` marked `IgnoreOnUpdate`. When adding an entity, audit every field against D365's update semantics.
- Composite-key **writes** (Update/Delete/batch) are live via an owned raw-`HttpClient` bypass in `ODataClientAdapter` (D365 returns `204 No Content` on a composite-key PATCH, so `ODataService.UpdateAsync` returns the caller's entity — a successful `Result<TEntity>` never carries a null `Value`).
- Before writing code examples with D365 entities, read the entity source to check which fields have `IgnoreOnCreate`/`IgnoreOnUpdate`.

## Property Naming and `[JsonPropertyName]`

- D365 F&O has ~479 camelCase fields (legacy X++ system fields like `dataAreaId`, `validFrom`, `validTo`, `recId`, `inventDimId`, `itemId`, `custAccount`, `transDate`) against ~19,604 PascalCase fields. Most fields are PascalCase; only the legacy system fields are camelCase.
- For camelCase OData fields, declare the CLR property in **PascalCase** (C# convention) and add `[JsonPropertyName("camelCaseName")]` to map it to the wire name. Example: `[JsonPropertyName("dataAreaId")] public required string DataAreaId { get; set; }`.
- **`[JsonPropertyName]` IS honoured by IntegratoR's filter / select / expand translator** (`IntegratoRODataExpressionTranslator` in `IntegratoR.OData.Common.Filters`). LINQ expressions like `x => x.DataAreaId == "USMF"` correctly emit `$filter=dataAreaId eq 'USMF'`.
- This is achieved via a copy-and-patch of PanoramicData.OData.Client's expression parser (MIT, attribution in `THIRD_PARTY_LICENSES.md`). The upstream library reads `MemberInfo.Name` directly and ignores `[JsonPropertyName]`. When the upstream PR adding attribute support is merged and released, the local translator can be deleted.
- Consumers should **never** need to write raw OData filter strings. Use strongly-typed LINQ expressions throughout.

## Service Layer

- `ODataService<T>` implements `IService<T>` — do NOT wrap it in a repository.
- `IODataBatchService<T>` for batch operations.
- Use `IMediator` to send commands/queries — never call services directly from endpoints.

## Configuration Binding

- DI binding: `services.Configure<ODataSettings>(configuration.GetSection("ODataSettings"))`.
- Programmatic: `services.AddODataClient(options => { options.Url = "..."; })`.
- `PostConfigure` composes via `IntegratoRBuilder.ConfigureOData(Action<ODataSettings>)`.
- All nested objects initialised with `= new()` — safe for `PostConfigure` lambdas.
