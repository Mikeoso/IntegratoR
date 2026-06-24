# Troubleshoot Common Issues

Real errors observed during framework development and resolution steps for each. Errors are grouped by where they surface in the stack — host startup, the OData layer, the OAuth layer, validation, and the dimension and journal smoke tests.

## Host Startup

### `ArgumentException: ODataSettings.Url must be set to a non-empty absolute URL`

The framework's normalisation guard fired because the configured `Url` was missing or whitespace.

**Resolution:**
1. Verify the configuration source — `local.settings.json` (development) or Azure App Settings (production).
2. The section name is **exactly** `ODataSettings` — case-sensitive.
3. On Azure App Settings the key is `ODataSettings__Url` (double-underscore separator).

### `KeyVault URI is not set in environment variables`

The production-host wiring requires `ClientSecretKeyVaultURI` as an environment variable. The check is intentional — without Key Vault, secrets cannot be sourced safely in production.

**Resolution:** set the env var to the Key Vault URI (e.g. `https://my-vault.vault.azure.net/`). For local development runs, ensure `local.settings.json` is loaded by `Program.cs` — the production path only fires when `EnvironmentName` is not `Development`.

### `Core Tools Version: 4.4.0` shown by `func start`

The local `func` CLI picked the in-process host, which does not support .NET 10 isolated workers.

**Resolution:** start `func` **without** the `--csharp` flag. The correct banner is `Core Tools Version: 4.9.0+...` and `Function Runtime Version: 4.1047.100…`. See [Run Smoke Tests](Run-Smoke-Tests).

## OData Layer

### `OData.AuthenticationFailed` with message *"Failed to acquire OAuth token for resource ..."*

`OAuthAuthenticator` could not acquire a bearer token via MSAL.

**Resolution:** check the wrapped MSAL error code embedded in the message:

| MSAL code | Cause | Fix |
|---|---|---|
| `AADSTS70011` | `Resource` is wrong | Set `Resource` to the environment URL **without** `/data` |
| `AADSTS50034` | Wrong tenant | Re-check `TenantId` is the tenant where the app is registered |
| `AADSTS7000215` | Invalid client secret | Rotate the secret in Azure AD, re-deploy via Key Vault |
| `AADSTS50012` | Secret hash mismatch | Secret was truncated or contains an invalid character — re-copy from Azure AD verbatim |

If the token is acquired but D365 still returns HTTP 401: register the app as a service user in D365 F&O (*System administration → Setup → Azure Active Directory applications*). See [Authentication Modes](Authentication-Modes).

### `OData.NotFound` with malformed-key URL in the message

D365 returned HTTP 404 because the request URL was malformed. Two common shapes:

1. URL contains `(System.Collections.Generic.Dictionary…)` — the composite-key write-path limitation. See [Known Limitations](Known-Limitations#composite-key-write-path).
2. URL is missing a path segment (`/fo`, `/data`) — `BaseAddress` trailing-slash issue.

**Resolution for (2):** ensure `ODataSettings.Url` ends with the correct path segment. The framework normalises by appending a trailing slash; either `https://host/fo` or `https://host/fo/` works. The host log emits `OData client configured with base URL: <normalised>` once at startup — verify the segment is present.

### `Warning: Delete on EntityName returned HTTP 404 and is being treated as success`

The ExceptionHandler's observability fix (since v1.3.4) surfacing a suppressed-404. The Result returned to the consumer is still `Result.Ok` (the `treatNotFoundAsSuccess` flag), but the warning signals one of:

- The entity was genuinely already gone (legitimate idempotent delete) — ignore the warning.
- The request URL was malformed and D365 had nothing to match (composite-key write path) — see [Known Limitations](Known-Limitations#composite-key-write-path).

The warning includes the request URL — `RequestUrl: (internal)` means the framework could not capture it; `RequestUrl: <full-url>` lets the operator distinguish the two cases.

### Polly retry warnings on every request

`IntegratoR.OData.HttpRetry` logger emits a `Warning` line per retry attempt with `RetryCount`, `DelayMs`, and `Reason`.

**Resolution:**

- Reason mentions `5xx` consistently → upstream (APIM or D365) is unhealthy; investigate downstream rather than increase `RetryCount`.
- Reason mentions `TaskCanceledException` → `Timeout` is too short; increase `ODataSettings.Timeout`.
- Reason mentions `400` → suspected Polly predicate over-broadening; see [Known Limitations](Known-Limitations#polly-retry-sometimes-retries-http-400).

## Validation

### `Validation.Error` returned with the first failed rule message

Expected behaviour. The framework's `ValidationBehaviour` surfaces only the first validation failure to keep client handling simple.

**Resolution:** if all failures need to surface, compose the messages into a single `RuleFor(...).Custom(...)` validator that builds a multi-line message. See [Add Validation](Add-Validation).

### Validator never runs

The validator's assembly is not registered with `AddConsumerHandlers(...)`.

**Resolution:** add the assembly explicitly: `integrator.AddConsumerHandlers(typeof(MyValidator).Assembly)`. The framework only scans assemblies passed in explicitly — it does **not** scan every loaded assembly.

## Dimension Smoke Test

### `DimensionParameters.NotFound` (introduced in v1.3.5)

`GetDimensionOrdersQuery` ran but `DimensionParameters.FindAll` returned no rows. The singleton row was never seeded in this D365 environment.

**Resolution:** browse to the *General ledger → Setup → Dimensions → Dimension parameters* page in D365 F&O. Seeding the row is a one-time setup operation per environment.

### Empty `Segments` list returned successfully

`DimensionIntegrationFormat.FindAsync` matched zero rows for the supplied `DimensionFormatName` + `HierarchyType`. The handler returns `Result.Ok` with an empty segment list rather than `NotFound` for backward compatibility.

**Resolution:** verify that the dimension format name exists in *General ledger → Setup → Financial dimensions → Dimension integration setup*, and that the `IsActive` flag is `Yes`. The query filter includes `IsActive == NoYes.Yes`.

### `Lines/any(l: l/Status eq 1)` rejected by D365

Pre-v1.3.5 the LINQ-to-OData translator did not intercept enum-constant comparisons inside `Any`/`All` lambda bodies, so it emitted the integer form D365 rejects with *"incompatible types ... 'Edm.Int32'"*.

**Resolution:** upgrade to v1.3.5 or later. The translator now emits the qualified-type form (`Microsoft.Dynamics.DataEntities.Status'Posted'`) for both top-level predicates and lambda bodies.

## LedgerJournal Smoke Test

### Step 4 (CreateDebitLine) fails with *"Das Feld 'Währung' muss ausgefüllt werden"* (or English equivalent)

D365 rejected the line create because `CurrencyCode` was missing from the payload.

**Resolution:** fixed in PR #92 (v1.3.3). The `CurrencyCode` attribute on `LedgerJournalLine` had `[ODataField(IgnoreOnCreate = true)]` removed — the value the consumer supplies now reaches the wire. Verify the consumer's code sets `CurrencyCode` on every line.

### Step 6 (UpdateHeader) fails with `LedgerJournalHeader.NotFound: Resource not found: LedgerJournalHeaders(System.Collections.Generic.Dictionary…)`

Composite-key write-path limitation. See [Known Limitations](Known-Limitations#composite-key-write-path).

### Step 7 (Cleanup.Delete*) returns success but the journal stays in D365

Same composite-key write-path limitation surfaced via the suppressed-404 observability warning. The journal must be deleted manually via the D365 UI until the bypass ships.

## Extending the Framework

### `InvalidOperationException: No service for type 'MediatR.IRequestHandler`2[...]' has been registered`

`mediator.Send(...)` was called with a command or query closed over a **custom or extended entity** (for example a subclass of `LedgerJournalLine`), but no closed handler was registered for it.

**Why:** MediatR v12 only closes the framework's open generic handlers (`CreateCommandHandler<T>`, the F&O `CreateLedgerJournalHeaderHandler<TEntity>` family, …) against entity types found in the **same assembly scan** that sets `RegisterGenericHandlers = true`. `AddIntegratoR` does this for the framework's own F&O entities, but `AddConsumerHandlers(...)` scans the consumer assembly with a plain registration — it does **not** close the generic handlers against consumer entity types. See [Known Limitations](Known-Limitations#consumer-entities-need-manual-generic-handler-registration).

**Resolution:** in the composition root, after `AddIntegratoR`, add a combined scan that includes the handler assemblies **and** the consumer assembly together:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterGenericHandlers = true;

    // open generic handlers — Application layer
    cfg.RegisterServicesFromAssembly(
        typeof(IntegratoR.Application.Features.Common.Commands.CreateCommandHandler<>).Assembly);

    // F&O open handlers (CreateLedgerJournalHeaderHandler<TEntity>, …)
    cfg.RegisterServicesFromAssembly(
        typeof(IntegratoR.OData.FO.Domain.Entities.LedgerJournal.LedgerJournalHeader).Assembly);

    // the assembly holding your extended entities
    cfg.RegisterServicesFromAssembly(typeof(MyLedgerJournalLine).Assembly);
});
```

The service layer (`IService<MyEntity>`) is **not** the problem here — it is registered as an open generic and resolves against any type. Only the MediatR handler closing is missing.

### A field I need on create or update is dropped from the payload (wrong `IgnoreOnCreate` / `IgnoreOnUpdate`)

An `[ODataField(IgnoreOnCreate = true)]` (or `IgnoreOnUpdate`) on a framework entity excludes a field your use case needs to send, so the value never reaches the wire.

**Resolution:** subclass the entity and **override the property, re-declaring the attribute** with the corrected flag. Overriding alone is not enough — `ODataFieldAttribute` is `Inherited = true`, so without re-declaring it the base value still applies:

```csharp
[Table("LedgerJournalLines")]
public class MyLedgerJournalLine : LedgerJournalLine
{
    [ODataField(IgnoreOnCreate = false)]
    [JsonPropertyName("AccountType")]
    public override LedgerJournalACType AccountType { get; set; }
}
```

The payload builder reflects on the **runtime type**, so an instance of the subclass picks up the override and the re-declared attribute wins (`AllowMultiple = false`). The property must be `virtual` — every `LedgerJournalLine` field is, except the server-generated `LineNumber` key, which you should never send on create anyway. Then register the subclass per the handler entry above.

If the attribute is wrong for **every** consumer (D365 actually accepts the field on create), fix it on the framework entity instead — see [Known Limitations](Known-Limitations#entity-attribute-audit-pending).

## When the Diagnostic Is Not Here

For any error not covered above:

1. Read the host log carefully — the framework logs the normalised `BaseAddress`, the OData request URL, the auth mode, and every retry attempt at startup-time and per-request.
2. Run the financial-dimension smoke test (read-only, low-risk) and inspect the per-step JSON response. It surfaces authentication, network, and OData configuration errors as typed `IntegrationError` codes without writing to D365.
3. Search the framework's source for the error code — codes follow the `<Subsystem>.<Cause>` convention (e.g. `OData.AuthenticationFailed`, `LedgerJournalHeader.NotFound`) and grep against the source typically finds where the code is constructed.
4. Open an issue at `https://github.com/Mikeoso/IntegratoR/issues` with the request URL, the `IntegrationError` shape, and the host log lines for the failing call.

## See Also

- [Run Smoke Tests](Run-Smoke-Tests) — the fastest diagnostic tool
- [Known Limitations](Known-Limitations) — limitations that look like bugs but have known status
- [Configure Resilience](Configure-Resilience) — Polly retry diagnostics
- [Authentication Modes](Authentication-Modes) — auth-failure deep dive
- [Handle Errors](Handle-Errors) — the `IntegrationError` shape that carries these diagnostics
