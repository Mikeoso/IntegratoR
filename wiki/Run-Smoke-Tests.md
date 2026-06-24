# Run Smoke Tests

`IntegratoR.SampleFunction` ships two HTTP triggers that exercise the full framework stack against a live D365 F&O sandbox. They are the fastest way to verify that authentication, OData wiring, the LINQ-to-OData translator, and the Polly resilience layer all cooperate correctly before integrating real business logic.

Both triggers are part of the open-source sample project — clone the repository, restore packages, and run them locally. They are not shipped as a separate NuGet package.

## What Each Trigger Exercises

| Trigger | Route | Operations | Side effects |
|---|---|---|---|
| `LedgerJournalSmokeTestTrigger` | `POST /api/smoke/ledger-journal` | Create → Get → Filter → Update → Delete on `LedgerJournalHeader` and `LedgerJournalLine` | Creates a real journal in D365; best-effort cleanup |
| `FinancialDimensionSmokeTestTrigger` | `POST /api/smoke/financial-dimensions` | `GetDimensionOrdersQuery` (one MediatR `Send` that chains two D365 reads) | **None** — read-only, safe to run repeatedly |

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

The journal smoke test exercises the write path — composite-key creation, payload field exclusion via `[ODataField]`, balanced debit/credit line creation, update, and cleanup.

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

The trigger runs seven steps in order:

| # | Step | What it proves |
|---|---|---|
| 1 | Create header | `CreateCommand<LedgerJournalHeader>` payload exclusion for `JournalBatchNumber` |
| 2 | Get by composite key | `GetByKeyQuery<LedgerJournalHeader>` composite-key construction |
| 3 | Filter by `dataAreaId` | `GetByFilterQuery` and the `[JsonPropertyName]`-aware filter translator |
| 4 | Create debit line | `CreateCommand<LedgerJournalLine>` with the required `CurrencyCode` |
| 5 | Create credit line | Same path, second line |
| 6 | Filter lines by `dataAreaId` | Translator against `LedgerJournalLine` |
| 7 | Cleanup (best effort) | Delete the lines and the header |

The response is the same per-step JSON shape as the financial-dimensions trigger.

> Step 7 (cleanup delete) currently has a known limitation — composite-key Update/Delete writes go through a code path with a parked PanoramicData issue. The cleanup step reports success in the response but logs a `Warning` line on the host saying the delete may not have happened. The created journal stays in the D365 sandbox until manually removed via the UI. See [Known Limitations](Known-Limitations#composite-key-write-path) for the parked workaround.

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
3. Run the ledger-journal smoke test (writes a journal, accept the orphan record)
4. Block promotion to production if either step's `Success` is `false`

The triggers signal failure via the JSON `Success` field, not the HTTP status code — the HTTP response is always 200 (unless the request body is malformed). CI scripts should check `.Success` against the response body (e.g. `jq -e '.Success'`), not rely on HTTP status alone.

## See Also

- [Troubleshoot Common Issues](Troubleshoot-Common-Issues) — diagnostic guide for the error codes above
- [Known Limitations](Known-Limitations) — composite-key write parking
- [Work with Dimensions](Work-with-Dimensions) — what the dimension smoke test exercises
- [Send Commands](Send-Commands) — what the journal smoke test exercises
