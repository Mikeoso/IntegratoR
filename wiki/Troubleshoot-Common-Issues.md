# Troubleshoot Common Issues
> Last verified against v2.0.1 (live-D365 items dated per run)

Each entry is keyed by the error you see. Match the symptom, read the one-line cause, apply the fix. Live-server behaviour is dated to the smoke run that proved it.

## HTTP 403 `ODataSecurityException` — "update not allowed for field 'X'"

D365 rejected an `UpdateCommand<T>` because the PATCH payload carried a field that is read-only on update. The offending field needs `[ODataField(IgnoreOnUpdate = true)]` so the payload builder omits it.

> [!WARNING]
> One read-only field in the payload makes D365 reject the **whole** PATCH with HTTP 403 — not only that field. The other fields never land. Mark every read-only field `IgnoreOnUpdate`, or the update fails entirely.

On `LedgerJournalHeader` these fields are read-only on update and already carry the attribute: `JournalName`, `IsPosted`, `JournalTotalDebit`, `JournalTotalCredit`, `AccountingCurrency`. If you extend the entity or add a new one, audit every property against D365's update semantics.

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new UpdateCommand<LedgerJournalHeader>(header), cancellationToken);

if (result.IsFailed)
{
    IntegrationError error = result.GetError();
    // On a stray read-only field the OData layer surfaces the 403 as a failed Result.
    // error.Type == ErrorType.Failure; error.Code == "LedgerJournalHeader.Unauthorized".
    // error.Exception is the wrapped ODataClientException — its Message and response body
    // carry D365's ODataSecurityException and the "update not allowed for field 'X'" detail.
}
```

To send a field D365 rejects, do not remove the attribute globally — subclass the entity and re-declare the property with the corrected flag (see [Errors when registering a custom entity](#errors-when-registering-a-custom-entity) below).

Verified against live D365 (JFI) on 2026-07-01: the five `LedgerJournalHeader` fields above were surfaced by an all-green LedgerJournal CRUD run once they were marked `IgnoreOnUpdate`.

## HTTP 401 with `ReasonPhrase "Authentication failed"`

`ODataAuthenticationHandler` could not attach a bearer token, so it short-circuited the request before it reached D365. The 401 carries the fixed generic phrase and no MSAL detail — tenant IDs and AADSTS codes stay server-side in the logged `IntegrationError`.

Check the failed `Result<T>` from the OAuth path — its `Code` is `Auth.Msal.{code}`, where `{code}` is the MSAL error code:

```csharp
if (result.IsFailed)
{
    IntegrationError error = result.GetError();
    // error.Code == "Auth.Msal.invalid_client"  (Auth.Msal.{MSAL error code})
    // error.Type == ErrorType.Failure; error.Message == "Token acquisition failed".
    // error.Exception carries the full MSAL detail for server-side logging.
}
```

Read the MSAL code on the inner exception in the host log and act on it:

| MSAL code | Cause | Fix |
|---|---|---|
| `AADSTS7000215` | Invalid client secret | Rotate the secret in Azure AD, re-deploy via Key Vault |
| `AADSTS50012` | Secret truncated or malformed | Re-copy the secret from Azure AD verbatim |
| `AADSTS50034` | Wrong tenant | Set `TenantId` to the tenant where the app is registered |
| `AADSTS70011` | Wrong resource | Set the resource to the environment URL **without** `/data` |

If the token is acquired but D365 still returns 401, register the app as a service user in D365 F&O (*System administration -> Setup -> Azure Active Directory applications*). See [Authentication Modes](Authentication-Modes).

## "Could not find a property named 'X'"

D365 rejected the filter, select, or payload because a camelCase wire field was declared without `[JsonPropertyName]`. About 479 legacy X++ fields (`dataAreaId`, `transDate`, `validFrom`, `recId`) are camelCase on the wire; the CLR property is PascalCase by C# convention, so the two must be bridged.

Add `[JsonPropertyName("camelCaseName")]` to the property. IntegratoR's filter/select/expand/`$orderby` translator honours the attribute, so a typed LINQ filter then emits the right wire name.

```csharp
[Key]
[JsonPropertyName("dataAreaId")]              // wire name is camelCase
[ODataField(IsRequired = true)]
public required string DataAreaId { get; set; }
```

With the attribute in place, `x => x.DataAreaId == "USMF"` emits `$filter=dataAreaId eq 'USMF'`. Without it, the translator reads the PascalCase CLR name and D365 fails the request. Never write raw OData filter strings — use typed LINQ throughout.

## A value you set on create is silently dropped

The property carries `[ODataField(IgnoreOnCreate = true)]`, so the payload builder omits it from the POST. The create succeeds, but your value never reached D365. On `LedgerJournalHeader`, `JournalBatchNumber` is `IgnoreOnCreate` because a D365 number sequence assigns it — read it back from `result.Value.JournalBatchNumber` after the create.

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(header), cancellationToken);

if (result.IsSuccess)
{
    // The server-assigned batch number is on the returned entity, not the one you sent.
    string batchNumber = result.Value.JournalBatchNumber!;
}
else
{
    IntegrationError error = result.GetError();
    // A rejected create fails the Result — inspect error.Code and error.Message for the D365 detail.
}
```

If you must send a field D365 accepts on create but the framework entity ignores, subclass and re-declare the property with `IgnoreOnCreate = false` (see below). If D365 accepts the field for every consumer, fix the attribute on the framework entity instead — see [Known Limitations](Known-Limitations).

## `Validation.Error` returned before any request reaches D365

`ValidationBehaviour` ran a registered `IValidator<TRequest>`, a rule failed, and the pipeline short-circuited. The handler never ran, so nothing reached D365. This is the framework working as intended, not a fault.

```csharp
if (result.IsFailed)
{
    IntegrationError error = result.GetError();
    // error.Code == "Validation.Error"; error.Type == ErrorType.Validation.
    // error.Message is the FIRST failing rule's message — later failures are dropped.
}
```

Read `error.Message` for the first failing rule and correct the input. To surface every failure at once, compose them in one `RuleFor(...).Custom(...)` rule that builds a multi-line message. See [Add Validation](Add-Validation).

## Your validator never fires

The assembly holding the validator was not passed to `AddConsumerHandlers`. `AddIntegratoR` scans only the assemblies you register explicitly plus the framework assemblies — it does not scan every loaded assembly.

Register the assembly in the configure delegate:

```csharp
services.AddIntegratoR(configuration, integrator =>
    integrator.AddConsumerHandlers(typeof(MyValidator).Assembly));
```

Generic command validators are closed over your entity types by the same scan (since v2.0.1), so registering the assembly also activates `CreateCommand<T>`/`UpdateCommand<T>` validation for your entities.

## Errors when registering a custom entity

`mediator.Send(...)` was called with a command or query closed over a custom or extended entity whose assembly was never handed to `AddConsumerHandlers`. MediatR then has no closed handler to resolve.

```text
InvalidOperationException: No service for type
'MediatR.IRequestHandler`2[...]' has been registered.
```

`AddIntegratoR` closes its generic handlers over an entity type only when that type's assembly is part of the combined handler scan. Register the assembly that holds your entity — nothing more:

```csharp
services.AddIntegratoR(configuration, integrator =>
    integrator.AddConsumerHandlers(typeof(MyLedgerJournalLine).Assembly));
```

`AddConsumerHandlers` folds the assembly into the scan, so `CreateCommand<MyLedgerJournalLine>` — and `Update`/`Delete`/`GetByKeyQuery`/`GetByFilterQuery` — resolves. The service layer was never the gap: `IService<T>` is an open-generic registration that resolves against any type.

To override an inherited `[ODataField]` flag, re-declare the property on the subclass — overriding alone is not enough, because `ODataFieldAttribute` is inherited:

```csharp
[Table("LedgerJournalLines")]
public class MyLedgerJournalLine : LedgerJournalLine
{
    [ODataField(IgnoreOnCreate = false)]      // re-declared flag wins
    [JsonPropertyName("AccountType")]
    public override LedgerJournalACType AccountType { get; set; }
}
```

The payload builder reflects on the runtime type, so an instance of the subclass picks up the re-declared attribute. The property must be `virtual` on the base entity.

## When the diagnostic is not here

For any error not listed above:

1. Read the host log — the framework logs the normalised base URL, the OData request URL, the auth mode, and every retry attempt.
2. Run the financial-dimension smoke test (read-only, low-risk); it surfaces auth, network, and OData configuration errors as typed `IntegrationError` codes without writing to D365. See [Run Smoke Tests](Run-Smoke-Tests).
3. Match on the `IntegrationError.Code` prefix — codes follow the `<Subsystem>.<Cause>` convention (`Auth.Msal.{code}`, `Validation.Error`).
4. Open an issue at [the IntegratoR repository](https://github.com/Mikeoso/IntegratoR/issues) with the request URL, the `IntegrationError` shape, and the failing host-log lines.

## See Also

- [Handle Errors](Handle-Errors) — the `IntegrationError` shape that carries these diagnostics
- [Run Smoke Tests](Run-Smoke-Tests) — the fastest live diagnostic
- [Authentication Modes](Authentication-Modes) — the auth-failure deep dive
- [Known Limitations](Known-Limitations) — behaviour that looks like a bug but has known status
