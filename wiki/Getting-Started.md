# Getting Started

```bash
dotnet add package IntegratoR.Abstractions
dotnet add package IntegratoR.Application
dotnet add package IntegratoR.OData
dotnet add package IntegratoR.OData.FO    # for D365 F&O
dotnet add package IntegratoR.RELion      # for RELion API
```

`IntegratoR.Abstractions` and `IntegratoR.Application` are required. The others are optional depending on your integration target. Core dependencies (FluentResults, MediatR, FluentValidation) are pulled in transitively.

## Configure the Connection

Add an `ODataSettings` section to `appsettings.json`:

```json
{
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "AuthMode": "OAuth",
    "ClientId": "your-azure-ad-app-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id",
    "Resource": "https://your-environment.operations.dynamics.com"
  }
}
```

See [[Configuration]] for the full property reference and authentication modes.

## Register Services

```csharp
using IntegratoR.Application.Common.Extensions;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.FO.Common.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices();                          // MediatR pipeline, validators, cache, auth
        services.AddODataClient(context.Configuration);             // OData HTTP client, Polly policies
        services.AddODataClientFOProxy(context.Configuration);      // D365 F&O handlers
    })
    .Build();

host.Run();
```

`AddApplicationServices()` must be called **first** — it registers MediatR pipeline behaviours in order: Logging -> Validation -> Caching -> Handler. See [[Azure-Functions-Host]] for a production-ready `Program.cs`.

You can also configure programmatically instead of using `appsettings.json`:

```csharp
services.AddODataClient(options =>
{
    options.Url = "https://your-environment.operations.dynamics.com/data";
    options.AuthMode = ODataAuthMode.OAuth;
    options.ClientId = "...";
    options.ClientSecret = "...";
    options.TenantId = "...";
    options.Resource = "https://your-environment.operations.dynamics.com";
});
```

## Define an Entity

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.OData.Common.Annotations;

namespace MyProject.Domain.Entities;

[Table("LedgerJournalHeaders")]
public class LedgerJournalHeader : BaseEntity<string>
{
    [Key]
    [JsonPropertyName("dataAreaId")]
    public required string DataAreaId { get; set; }

    [Key]
    [JsonPropertyName("JournalBatchNumber")]
    [ODataField(IgnoreOnCreate = true)]         // server-generated
    public string? JournalBatchNumber { get; set; }

    [JsonPropertyName("JournalName")]
    public required string JournalName { get; set; }

    [JsonPropertyName("Description")]
    public required string Description { get; set; }

    public override object[] GetCompositeKey()
    {
        return [DataAreaId, JournalBatchNumber ?? "null"];
    }
}
```

See [[Entities]] for `BaseEntity<TKey>`, `ODataFieldAttribute`, and custom entity patterns.

## Send a Command

```csharp
var header = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "GenJrn",
    Description = "Monthly accruals - March 2026"
};

var command = new CreateCommand<LedgerJournalHeader>(header);
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);

if (result.IsSuccess)
{
    LedgerJournalHeader created = result.Value;
    // created.JournalBatchNumber is now "JBN-000431" (server-generated)
}
```

`UpdateCommand<T>` and `DeleteCommand<T>` follow the same pattern. See [[Commands]] for details.

## Run a Query

```csharp
// By composite key
var query = new GetByKeyQuery<LedgerJournalHeader>(["USMF", "JBN-000431"]);
Result<LedgerJournalHeader> result = await mediator.Send(query, cancellationToken);
// result.Value contains the matching entity

// By filter expression
var filter = new GetByFilterQuery<LedgerJournalHeader>(
    h => h.DataAreaId == "USMF" && h.JournalName == "GenJrn");
Result<IEnumerable<LedgerJournalHeader>> results = await mediator.Send(filter, cancellationToken);
// results.Value contains all matching entities
```

See [[Queries]] for filter syntax and return types.

## What Just Happened

The steps above assembled a working integration pipeline:

1. **NuGet packages** brought in the framework and its dependencies (MediatR, FluentResults, FluentValidation, Polly).
2. **`ODataSettings`** configured the HTTP connection and OAuth credentials for your D365 F&O environment.
3. **`AddApplicationServices()`** registered the MediatR pipeline (Logging -> Validation -> Caching -> Handler), cache service, and OAuth authenticator.
4. **`AddODataClient()`** registered the generic OData HTTP client with Polly retry/circuit-breaker policies.
5. **`AddODataClientFOProxy()`** registered the D365 F&O-specific entity handlers and validators.
6. **The entity** mapped C# properties to OData JSON fields, with `[ODataField(IgnoreOnCreate = true)]` excluding server-generated values from the POST payload.
7. **`CreateCommand<T>`** sent the entity through the full pipeline — logging the request, validating the entity, then POSTing to D365 and returning a `Result<T>` with the server-populated fields.

## When Things Go Wrong

**Wrong credentials or expired secret:**

```csharp
Result<LedgerJournalHeader> result = await mediator.Send(command, cancellationToken);
// result.IsFailed == true
// result.GetError()?.Code    == "OData.AuthenticationFailed"
// result.GetError()?.Message == "Failed to acquire OAuth token for resource ..."
```

Check that `ClientId`, `ClientSecret`, `TenantId`, and `Resource` in `ODataSettings` are correct. Secrets expire — rotate them in Azure Key Vault.

**Missing or wrong `ODataSettings` section:**

The host throws at startup if `Url` is null or empty. Double-check your `appsettings.json` section name matches `"ODataSettings"` exactly.

**Entity validation failure:**

```csharp
var invalid = new LedgerJournalHeader
{
    DataAreaId = "USMF",
    JournalName = "",           // empty — fails NotEmpty rule
    Description = "Test"
};

Result<LedgerJournalHeader> result = await mediator.Send(
    new CreateCommand<LedgerJournalHeader>(invalid), cancellationToken);
// result.IsFailed          == true
// result.GetError()?.Code  == "Validation.Error"
// result.GetError()?.Type  == ErrorType.Validation
```

The pipeline short-circuits before reaching D365. See [[Error-Handling]] for the full error model.

## See Also

- [[Azure-Functions-Host]] — production-ready host with Key Vault and Application Insights
- [[Entities]] — entity definition patterns and `ODataFieldAttribute`
- [[Commands]] — create, update, and delete operations
- [[Configuration]] — full settings reference
