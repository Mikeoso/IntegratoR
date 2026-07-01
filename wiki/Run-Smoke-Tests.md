# Run Smoke Tests

`IntegratoR.SampleFunction` ships two HTTP triggers that exercise the full framework stack against a live D365 F&O sandbox. They are the fastest way to verify that authentication, OData wiring, the LINQ-to-OData translator, and the Polly resilience layer all cooperate correctly before integrating real business logic.

Both triggers are part of the open-source sample project — clone the repository, restore packages, and run them locally. They are not shipped as a separate NuGet package.

## What Each Trigger Exercises

| Trigger | Route | Operations | Side effects |
|---|---|---|---|
| `LedgerJournalSmokeTestTrigger` | `POST /api/smoke/ledger-journal` | Create → Get → Filter → Update → Delete on `LedgerJournalHeader` and `LedgerJournalLine` | Creates a real journal in D365; deletes it again on the same run — self-cleaning, no orphan left behind |
| `FinancialDimensionSmokeTestTrigger` | `POST /api/smoke/financial-dimensions` | `GetDimensionOrdersQuery` (one MediatR `Send` that chains two D365 reads) | **None** — read-only, safe to run repeatedly |

Both triggers use `AuthorizationLevel.Function`. Running locally with `func start` no host key is required; once deployed to Azure they need a function/host key (`?code=<key>` or the `x-functions-key` header).

The financial-dimension trigger is the recommended first run: it is read-only, depends on no company context, and verifies authentication + OData + the `[JsonPropertyName]` filter translator in one round-trip.

## Run the Triggers Locally

The triggers require Azurite for storage emulation (the Azure Functions worker reads secrets from Blob Storage even when all triggers are HTTP) and the standard isolated-worker host:

```bash
# 1. Start Azurite in a separate terminal
azurite --silent --location /tmp/azurite-data \
        --blobHost 127.0.0.1 --queueHost 127.0.0.1 --tableHost 127.0.0.1

# 2. Build and publish the SampleFunction
dotnet publish IntegratoR.SampleFunction \
   -c Debug -o IntegratoR.SampleFunction/bin/output

# 3. Copy local.settings.json into the publish output (csproj sets CopyToPublishDirectory=Never)
cp IntegratoR.SampleFunction/local.settings.json IntegratoR.SampleFunction/bin/output/

# 4. Start the func host in the publish directory
cd IntegratoR.SampleFunction/bin/output
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated func start --port 7123
```

The host banner should show:

```
Core Tools Version:       4.9.0+...
Function Runtime Version: 4.1047.100.....
```

If the version is `4.4.0` instead, the in-process host has been picked up — that variant does not support .NET 10 isolated workers. Restart `func start` **without** the `--csharp` flag.

## Financial Dimension Smoke Test

```bash
curl -s -X POST http://localhost:7123/api/smoke/financial-dimensions \
     -H "Content-Type: application/json" \
     -d '{"DimensionFormatName":"Sachkontodimensionen","HierarchyType":"DataEntityLedgerDimensionFormat"}'
```

Request body:

```json
{
  "DimensionFormatName": "Sachkontodimensionen",
  "HierarchyType": "DataEntityLedgerDimensionFormat"
}
```

| Field | Type | Notes |
|---|---|---|
| `DimensionFormatName` | string | Must match a row in D365 `DimensionIntegrationFormats` |
| `HierarchyType` | string (enum name) | `JsonStringEnumConverter` accepts the enum **name** (e.g. `"DataEntityLedgerDimensionFormat"`) — numeric values also work but the name is more readable |

Successful response:

```json
{
  "Success": true,
  "Delimiter": "-",
  "Segments": ["MainAccount", "A_Kostenstelle", "B_Segment", "C_Profitcenter",
               "D_Projekte", "E_Artikel_PSP", "F_Debitor", "G_Bewegungsarten",
               "H_Partnergesellschaft"],
  "Steps": [
    {
      "Name": "GetDimensionOrders",
      "Success": true,
      "ErrorCode": null,
      "ErrorType": null,
      "ErrorMessage": null,
      "Details": "Delimiter='-', Segments=[MainAccount, A_Kostenstelle, ...]"
    }
  ]
}
```

The exact segment list depends on the D365 environment — the example above is the response captured against a sandbox configured with nine dimension segments separated by hyphens. The trigger executes a single MediatR `Send(...)` for `GetDimensionOrdersQuery`, which chains two D365 reads (`DimensionIntegrationFormat.FindAsync` plus `DimensionParameters.FindAll`) and returns roughly in 1–4 seconds depending on APIM warm state.

A failed response surfaces the `IntegrationError` per step:

```json
{
  "Success": false,
  "Delimiter": null,
  "Segments": null,
  "Steps": [
    {
      "Name": "GetDimensionOrders",
      "Success": false,
      "ErrorCode": "OData.AuthenticationFailed",
      "ErrorType": "Failure",
      "ErrorMessage": "Failed to acquire OAuth token for resource ..."
    }
  ]
}
```

## LedgerJournal Smoke Test

The journal smoke test exercises the full write path end to end — composite-key creation, payload field exclusion via `[ODataField]`, balanced debit/credit line creation, **composite-key Update and Delete**, and re-read verification that each write landed. It was verified green against a live D365 (JFI) sandbox on 2026-07-01: the complete create → update → delete → verify-gone cycle passed and self-cleaned with no orphan record.

```bash
curl -s -X POST http://localhost:7123/api/smoke/ledger-journal \
     -H "Content-Type: application/json" \
     -d '{
       "Company":                 "USMF",
       "JournalName":             "GenJrn",
       "AccountDisplayValue":     "110180-",
       "OffsetAccountDisplayValue": "211100-",
       "Amount":                  100.00,
       "CurrencyCode":            "USD"
     }'
```

The trigger runs the full ordered write-and-verify chain:

| Step | What it proves |
|---|---|
| `CreateHeader` | `CreateCommand<LedgerJournalHeader>` payload exclusion for `JournalBatchNumber` |
| `GetHeaderByKey` | `GetByKeyQuery<LedgerJournalHeader>` composite-key construction |
| `FilterHeaderByDataAreaId` | `GetByFilterQuery` and the `[JsonPropertyName]`-aware filter translator |
| `CreateDebitLine` | `CreateCommand<LedgerJournalLine>` with the required `CurrencyCode` |
| `CreateCreditLine` | Same path, second line |
| `FilterLinesByDataAreaId` | Translator against `LedgerJournalLine` |
| `UpdateHeader` | `UpdateCommand<LedgerJournalHeader>` composite-key **PATCH** via the owned bypass |
| `VerifyHeaderUpdated` | Re-reads the header by composite key and confirms the new `Description` |
| `UpdateLine` | `UpdateCommand<LedgerJournalLine>` composite-key PATCH on the line |
| `VerifyLineUpdated` | Re-reads the line and confirms the updated `TransactionText` |
| `DeleteLine[…]` | `DeleteCommand<LedgerJournalLine>` composite-key **DELETE** per line |
| `DeleteHeader` | `DeleteCommand<LedgerJournalHeader>` composite-key DELETE |
| `VerifyHeaderDeleted` | Re-reads the header; a `NotFound` result confirms the delete landed |

The response is the same per-step JSON shape as the financial-dimensions trigger.

Composite-key Update and Delete run through the owned raw-`HttpClient` bypass in `ODataClientAdapter` (shipped in v2.0.0) — it builds the keyed URL manually, e.g. `LedgerJournalHeaders(dataAreaId='1210',JournalBatchNumber='LNR0000300')`, through the named `"ODataClient"` client so the write carries the same auth, Polly resilience, and `BaseAddress` as every other request. Because the chain deletes everything it creates and verifies the header is gone, a green run leaves no record behind.

## Diagnosing a Failed Smoke Test

The per-step `Steps[]` array surfaces the failing operation and the `IntegrationError` it produced. Common failure shapes:

| `ErrorCode` | `ErrorType` | Likely cause |
|---|---|---|
| `OData.AuthenticationFailed` | `Failure` | OAuth credentials wrong, expired, or service principal lacks API access |
| `OData.NotFound` | `NotFound` | Wrong `Url` (path segment missing), wrong `[Table]` attribute on the entity, or company/journal name does not exist in D365 |
| `SmokeTest.MissingFields` | `Validation` | Required field in the request body is empty |
| `SmokeTest.InvalidJson` | `Validation` | Request body is not valid JSON |
| `DimensionParameters.NotFound` | `NotFound` | The dimension singleton row is missing in this environment (very rare) |

The full diagnostic chain — host log lines, retry warnings, circuit breaker state — is in the `func start` output. See [Troubleshoot Common Issues](Troubleshoot-Common-Issues) for resolutions to specific errors.

## Use in CI

Both triggers can be wired into a CI smoke pipeline that exercises a sandbox after every deployment. Recommended pattern:

1. Deploy the framework to a sandbox slot
2. Run the financial-dimension smoke test (read-only, fast, low-risk)
3. Run the ledger-journal smoke test (writes a journal, then deletes it — self-cleaning, no orphan left behind)
4. Block promotion to production if either step's `Success` is `false`

The triggers signal failure via the JSON `Success` field, not the HTTP status code — the HTTP response is always 200 (unless the request body is malformed). CI scripts should check `.Success` against the response body (e.g. `jq -e '.Success'`), not rely on HTTP status alone.

## See Also

- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — diagnostic guide for the error codes above
- [Known Limitations](Known-Limitations) — remaining open items (composite-key writes are resolved)
- [Work with Dimensions](Work-with-Dimensions) — what the dimension smoke test exercises
- [Send Commands](Send-Commands) — what the journal smoke test exercises
