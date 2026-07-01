# Run Smoke Tests
> Last verified against v2.0.1

`IntegratoR.SampleFunction` ships two HTTP triggers that drive the whole stack — auth, OData wiring, the LINQ-to-OData translator, Polly resilience — against a live D365 F&O sandbox. Run them to prove a fresh deployment works before you wire in real business logic. They are part of the sample host, not a separate NuGet package.

Start with the read-only dimension trigger: it needs no company context and leaves no records behind.

```bash
curl -s -X POST http://localhost:7123/api/smoke/financial-dimensions \
     -H "Content-Type: application/json" \
     -d '{"DimensionFormatName":"Sachkontodimensionen","HierarchyType":"DataEntityLedgerDimensionFormat"}'
```

A green run returns the delimiter and ordered segments the handler parsed out of D365:

```json
{
  "Success": true,
  "Delimiter": "-",
  "Segments": ["MainAccount", "A_Kostenstelle", "C_Profitcenter"],
  "Steps": [
    {
      "Name": "GetDimensionOrders",
      "Success": true,
      "ErrorCode": null,
      "ErrorType": null,
      "ErrorMessage": null,
      "Details": "Delimiter='-', Segments=[MainAccount, A_Kostenstelle, C_Profitcenter]"
    }
  ]
}
```

The exact segment list depends on the target environment — the example is the shape captured against a JFI sandbox.

## The two triggers

| Function ID | Route | Flow | Side effects |
|---|---|---|---|
| `FinancialDimensionSmokeTest_HTTPTrigger` | `POST /api/smoke/financial-dimensions` | One `GetDimensionOrdersQuery` (chains two D365 reads) | None — read-only, safe to repeat |
| `LedgerJournalSmokeTest_HTTPTrigger` | `POST /api/smoke/ledger-journal` | Create → GetByKey → Filter → Update → Delete on `LedgerJournalHeader` + `LedgerJournalLine` | Writes a real journal, then deletes it — self-cleaning on a green run |

Both use `AuthorizationLevel.Function`. Locally under `func start` no key is needed; once deployed to Azure they need a function/host key (`?code=<key>` or the `x-functions-key` header).

## Run the triggers locally

The host needs Azurite for storage emulation (the Functions host needs an `AzureWebJobsStorage` account for its own runtime state even when every trigger is HTTP) plus the standard isolated-worker `func` host:

```bash
# 1. Start Azurite in a separate terminal
azurite --silent --location /tmp/azurite-data \
        --blobHost 127.0.0.1 --queueHost 127.0.0.1 --tableHost 127.0.0.1

# 2. Build and publish the SampleFunction
dotnet publish IntegratoR.SampleFunction -c Debug -o IntegratoR.SampleFunction/bin/output

# 3. Copy local.settings.json into the publish output (csproj sets CopyToPublishDirectory=Never)
cp IntegratoR.SampleFunction/local.settings.json IntegratoR.SampleFunction/bin/output/

# 4. Start the func host in the publish directory
cd IntegratoR.SampleFunction/bin/output
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated func start --port 7123
```

> [!NOTE]
> `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` selects the out-of-process host that .NET 10 needs; the in-process host does not support isolated workers. If `func` still starts the in-process host, force the out-of-process one with `func start --runtime default --port 7123`.

## Financial dimension smoke test

POST a `DimensionFormatName` and `HierarchyType` that exist in the target sandbox. The trigger sends one `GetDimensionOrdersQuery`, which chains a `DimensionIntegrationFormat` filter with a `DimensionParameters` find-all and returns the parsed delimiter and segments.

```json
{
  "DimensionFormatName": "Sachkontodimensionen",
  "HierarchyType": "DataEntityLedgerDimensionFormat"
}
```

| Field | Type | Notes |
|---|---|---|
| `DimensionFormatName` | string (required) | Must match a `DimensionIntegrationFormat` row in D365 |
| `HierarchyType` | string (enum name) | The global `JsonStringEnumConverter` binds the `DimensionHierarchyType` **name** (e.g. `"DataEntityLedgerDimensionFormat"`) |

No company context is required — the dimension metadata entities are global, not per-`DataAreaId`.

## Ledger journal smoke test

The journal trigger drives the full write path: composite-key create, `[ODataField]` payload exclusion, a balanced debit/credit line pair, composite-key Update and Delete, and re-read verification that each write landed.

```bash
curl -s -X POST http://localhost:7123/api/smoke/ledger-journal \
     -H "Content-Type: application/json" \
     -d '{
       "Company":                   "USMF",
       "JournalName":               "GenJrn",
       "AccountDisplayValue":       "110180-",
       "OffsetAccountDisplayValue": "211100-",
       "Amount":                    100.00,
       "CurrencyCode":              "USD"
     }'
```

`Company` and `JournalName` are required; an empty either returns `SmokeTest.MissingFields` (`Validation`). The header the trigger builds uses the current non-generic `BaseEntity` — `LedgerJournalHeader` overrides `GetCompositeKey() => [DataAreaId, JournalBatchNumber!]`, so the framework constructs the keyed URL for you.

The ordered chain, each step a `Result<T>` inspected in turn:

| Step | What it proves |
|---|---|
| `CreateHeader` | `CreateCommand<LedgerJournalHeader>`; `JournalBatchNumber` excluded on create (server-assigned) |
| `GetHeaderByKey` | `GetByKeyQuery<LedgerJournalHeader>` composite-key construction |
| `FilterHeaderByDataAreaId` | `GetByFilterQuery` + the `[JsonPropertyName]`-aware translator (`dataAreaId` camelCase) |
| `CreateDebitLine` / `CreateCreditLine` | `CreateCommand<LedgerJournalLine>` with required `CurrencyCode` |
| `FilterLinesByDataAreaId` | Translator against `LedgerJournalLine` |
| `UpdateHeader` / `VerifyHeaderUpdated` | Composite-key PATCH via the owned bypass, then re-read confirms the new `Description` |
| `UpdateLine` / `VerifyLineUpdated` | Line PATCH sets `TransactionText` (wire `Text`), then re-read confirms it |
| `DeleteLine[…]` / `DeleteHeader` | Composite-key DELETE — lines first, header last (D365 rejects deleting a header with child lines) |
| `VerifyHeaderDeleted` | Re-reads the header; a `NotFound` result confirms the delete landed |

Composite-key Update and Delete run through the owned raw-`HttpClient` bypass in `ODataClientAdapter` (since v2.0.0). It builds the keyed URL manually — `LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B0001')` — through the named `"ODataClient"` client, so the write carries the same auth, Polly resilience, and `BaseAddress` as every other call. Because the chain deletes what it creates and verifies the header is gone, a green run leaves no orphan.

Verified against live D365 (JFI) on 2026-07-01: the complete create → update → delete → verify-gone cycle passed and self-cleaned.

> [!NOTE]
> D365 answers a composite-key PATCH with `204 No Content`, so `UpdateCommand` returns your caller entity and `result.Value` may be null on a successful write. The step builder null-guards `result.Value` before projecting details — a diagnostic trigger must never throw and lose every per-step result to a 500.

## Read a failed step

Each entry in `Steps[]` carries the failing operation and its `IntegrationError`. On failure the trigger surfaces the `Code` and `Type`, and returns a generic `ErrorMessage` — the full server detail is logged host-side only, never echoed to the caller.

```json
{
  "Success": false,
  "CreatedJournalBatchNumber": null,
  "Steps": [
    {
      "Name": "CreateHeader",
      "Success": false,
      "ErrorCode": "Auth.Msal.invalid_client",
      "ErrorType": "Failure",
      "ErrorMessage": "Operation failed; see host logs for details."
    }
  ]
}
```

Common failure shapes:

| `ErrorCode` | `ErrorType` | Likely cause |
|---|---|---|
| `Auth.Msal.{code}` | `Failure` | OAuth token acquisition failed — wrong/expired client secret, or the service principal lacks API access. (An auth short-circuit on the HTTP path surfaces as a 401 with `ReasonPhrase "Authentication failed"`.) |
| `SmokeTest.MissingFields` | `Validation` | A required request-body field is empty |
| `SmokeTest.InvalidJson` | `Validation` | The request body is not valid JSON |
| `<Entity>.NotFound` | `NotFound` | Wrong `Url` path segment, wrong `[Table]` attribute, or the company/journal name does not exist |

> [!WARNING]
> An `UpdateHeader` step that fails with HTTP 403 (`ODataSecurityException`, "update not allowed for field 'X'") means a read-only field entered the PATCH payload. On `LedgerJournalHeader`, `JournalName`, `AccountingCurrency`, `IsPosted`, `JournalTotalDebit`, and `JournalTotalCredit` are `IgnoreOnUpdate`; a single such field in the payload makes D365 reject the whole PATCH.

The full diagnostic chain — retry warnings, circuit-breaker state, the logged server message — is in the `func start` output. See [Troubleshoot Common Issues](Troubleshoot-Common-Issues) for resolutions.

## Wire into CI

Both triggers signal outcome through the JSON `Success` field, not the HTTP status — the response is `200 OK` unless the body is malformed (`400`). A CI script must check `.Success` against the body, not the status code:

1. Deploy to a sandbox slot.
2. Run the financial-dimension test (read-only, fast).
3. Run the ledger-journal test (writes then self-cleans).
4. Block promotion if either body's `Success` is `false` — for example, `jq -e '.Success'`.

## See Also

- [Send Commands](Send-Commands)
- [Work with Dimensions](Work-with-Dimensions)
- [Handle Errors](Handle-Errors)
- [Troubleshoot Common Issues](Troubleshoot-Common-Issues)
