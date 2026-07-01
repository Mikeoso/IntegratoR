# Getting Started
> Last verified against v2.0.1

By the end of this page you will have an Azure Functions isolated-worker host that sends a `CreateCommand<LedgerJournalHeader>` to D365 F&O and reads back the server-assigned `JournalBatchNumber`.

## Prerequisites

- .NET 10 SDK (preview channel).
- An Azure Functions isolated-worker project. The in-process model is not supported.
- A D365 F&O environment plus an Azure AD app registration with OData access.
- An OAuth client secret, or an Azure API Management subscription key.

## 1. Install

```bash
dotnet add package IntegratoR.Hosting
```

`IntegratoR.Hosting` is the composition root; it pulls in the Application, OData, OData.FO, and Abstractions layers transitively.

## 2. Configure

Add an `ODataSettings` section to `local.settings.json`. Connection settings sit at the root, credentials under `Authentication`, resilience under `Resilience`. The placeholders below come from your D365 environment and Azure AD app registration.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  },
  "ODataSettings": {
    "Url": "https://your-environment.operations.dynamics.com/data",
    "Authentication": {
      "Mode": "OAuth",
      "OAuth": {
        "ClientId": "<azure-ad-app-registration-client-id>",
        "ClientSecret": "<client-secret>",
        "TenantId": "<azure-ad-tenant-id>",
        "Resource": "https://your-environment.operations.dynamics.com"
      }
    }
  }
}
```

Wire the framework with one line in `Program.cs`:

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly());
        });
    })
    .Build();

host.Run();
```

`AddIntegratoR` is the only entry point you call. It registers the MediatR pipeline, the OData client with Polly resilience, the OAuth authenticator, and every bundled D365 F&O handler. `AddConsumerHandlers` closes the generic handlers and validators over any entities defined in your own assembly.

> [!NOTE]
> On Azure App Settings, express JSON nesting with double underscores: `ODataSettings__Authentication__OAuth__ClientId`. `Mode` accepts the string `"OAuth"` or `"ApiKey"` and has no safe default — set it explicitly.

## 3. Send your first command

`LedgerJournalHeader` ships in `IntegratoR.OData.FO`, so no custom class is needed. Inject `IMediator`, build the entity with a real company (`DataAreaId "USMF"`), and send a `CreateCommand<LedgerJournalHeader>`. Leave `JournalBatchNumber` unset — it carries `[ODataField(IgnoreOnCreate = true)]` because D365 assigns it from a number sequence.

```csharp
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

public sealed class CreateJournalFunction(IMediator mediator, ILogger<CreateJournalFunction> logger)
{
    [Function("CreateJournal")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LedgerJournalHeader header = new()
        {
            DataAreaId = "USMF",
            JournalName = "GenJrn",
            Description = "Monthly accruals — March 2026"
        };

        Result<LedgerJournalHeader> result = await mediator
            .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
            .ConfigureAwait(false);
```

## 4. Verify the result

Every framework operation returns `Result<T>`. Business failures never throw — inspect `result.IsSuccess` and read the typed `IntegrationError` through `result.GetError()`.

```csharp
        if (result.IsSuccess)
        {
            // D365 populates JournalBatchNumber on the returned entity.
            logger.LogInformation("Created journal {BatchNumber}", result.Value.JournalBatchNumber);

            HttpResponseData ok = req.CreateResponse(HttpStatusCode.Created);
            await ok.WriteAsJsonAsync(result.Value, cancellationToken).ConfigureAwait(false);
            return ok;
        }

        IntegrationError? error = result.GetError();
        logger.LogWarning("Create failed: [{Code}] {Message}", error?.Code, error?.Message);

        HttpResponseData fail = req.CreateResponse(HttpStatusCode.BadRequest);
        await fail.WriteAsJsonAsync(new { error?.Code, error?.Message }, cancellationToken).ConfigureAwait(false);
        return fail;
    }
}
```

On success, `result.Value.JournalBatchNumber` holds the number sequence value D365 assigned. On failure, `error.Code` and `error.Type` tell you what happened: bad OAuth credentials surface as code `Auth.Msal.{code}` with `ErrorType.Failure`; a validation failure surfaces as `Validation.Error` with `ErrorType.Validation`.

> [!WARNING]
> An OAuth token-acquisition failure short-circuits the HTTP pipeline with **401** and the generic `ReasonPhrase "Authentication failed"` — no tenant IDs or MSAL codes leak to the caller. The full MSAL detail stays server-side in the logged `IntegrationError`.

## What just happened

`mediator.Send(...)` ran the request through the pipeline in a fixed order, then the OData layer talked to D365:

1. `LoggingBehaviour` logged the command type and started the duration timer.
2. `ValidationBehaviour` ran the FluentValidation validators registered for `CreateCommand<LedgerJournalHeader>`.
3. `CachingBehaviour` checked for an `ICacheableQuery<T>` marker; commands are never cached, so it passed through.
4. `CreateCommandHandler<LedgerJournalHeader>` serialised the entity (omitting `JournalBatchNumber`) and POSTed it to the `LedgerJournalHeaders` set.

Before the request reached D365, the `ODataAuthenticationHandler` acquired an OAuth bearer token via MSAL (cached with proactive refresh), and Polly wrapped the call with retry on transient status codes and a circuit breaker. The response deserialised into a fresh `LedgerJournalHeader` — `JournalBatchNumber` populated — wrapped in a successful `Result<T>`.

## Run the full sample

`IntegratoR.SampleFunction` is a clone-and-run host with two HTTP triggers that exercise the pipeline against a live D365 sandbox:

- `POST smoke/ledger-journal` runs create, get-by-key, filter, update, and delete across `LedgerJournalHeader` and `LedgerJournalLine`, returning a per-step JSON breakdown.
- `POST smoke/financial-dimensions` runs `GetDimensionOrdersQuery` against the dimension metadata entities.

See [Run Smoke Tests](Run-Smoke-Tests) for request bodies and expected responses.

## See Also

- [Configure OData](Configure-OData)
- [Define Entities](Define-Entities)
- [Send Commands](Send-Commands)
- [Set Up Azure Functions Host](Set-Up-Azure-Functions-Host)
