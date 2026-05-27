# Integrate with RELion

`IntegratoR.RELion` is an optional package that wires up an HTTP client and MediatR handlers for the RELion property-management API. The module follows the same `Result<T>` + CQRS conventions as the rest of the framework but lives in its own composition root — consumers needing only D365 integration never load it.

## Install and Register

```bash
dotnet add package IntegratoR.RELion
```

`AddRelionClient` registers everything the module needs: settings binding, the authentication delegating handler, the typed HTTP client, the `IRelionService` implementation, and MediatR handlers from the RELion assembly.

```csharp
using IntegratoR.RELion.Common.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddIntegratoR(context.Configuration, integrator =>
        {
            integrator.AddConsumerHandlers(Assembly.GetExecutingAssembly());
        });

        // RELion is a separate registration — AddIntegratoR does not wire it.
        services.AddRelionClient(context.Configuration);
    })
    .Build();
```

`AddRelionClient` reads the `RelionSettings` section from configuration and registers the named HTTP client `"RelionApiClient"` with the `RelionAuthenticationHandler` in its delegating chain.

## Configuration

```json
{
  "RelionSettings": {
    "Url": "https://your-relion-environment.example.com/api",
    "Timeout": 120,
    "Company": "1210",
    "AuthMode": "ApiKey",
    "SubscriptionKey": "<apim-subscription-key>",
    "SubscriptionHeaderKey": "Ocp-Apim-Subscription-Key",
    "ClientId": "<oauth-client-id>",
    "ClientSecret": "<oauth-client-secret>",
    "TenantId": "<azure-ad-tenant-id>",
    "Resource": "<oauth-resource-uri>"
  }
}
```

| Property | Type | Purpose |
|---|---|---|
| `Url` | string | Base URL of the RELion API endpoint (required) |
| `Timeout` | int | HTTP request timeout in seconds (default `120`) |
| `Company` | string | RELion company identifier to scope API requests to |
| `AuthMode` | `RelionAuthMode` | Either `ApiKey` (APIM) or `OAuth` |
| `SubscriptionKey` | string | APIM subscription key — used when `AuthMode == ApiKey` |
| `SubscriptionHeaderKey` | string | Header name carrying the subscription key |
| `ClientId` / `ClientSecret` / `TenantId` / `Resource` | string | OAuth credentials — used when `AuthMode == OAuth` |

> The RELion settings still use the **flat** structure inherited from earlier framework releases. A restructuring to mirror the nested `Authentication` + `Resilience` layout used by `ODataSettings` is on the backlog — see [Known Limitations](Known-Limitations).

## Built-In Queries

The module ships with at least one ready-made query that demonstrates the pattern:

```csharp
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;

Result<RelionLedgerAccountMapping> result = await mediator.Send(
    new GetRelionLedgerAccountMappingQuery(/* parameters */),
    cancellationToken).ConfigureAwait(false);
```

The handler delegates to `IRelionService`, which composes the HTTP request and deserialises the response into the strongly-typed domain model (`RelionLedgerAccountMapping`).

## Custom Queries Against RELion

The same `IQuery<TResponse>` / `IRequestHandler` pattern used elsewhere in the framework applies here. A custom RELion query:

```csharp
using FluentResults;
using IntegratoR.Abstractions.Interfaces.Queries;

public record GetRelionCompaniesQuery() : IQuery<Result<IEnumerable<RelionCompany>>>;
```

Pair with a handler that injects `IRelionService`:

```csharp
public sealed class GetRelionCompaniesQueryHandler
    : IRequestHandler<GetRelionCompaniesQuery, Result<IEnumerable<RelionCompany>>>
{
    private readonly IRelionService _service;

    public GetRelionCompaniesQueryHandler(IRelionService service)
    {
        _service = service;
    }

    public async Task<Result<IEnumerable<RelionCompany>>> Handle(
        GetRelionCompaniesQuery request,
        CancellationToken cancellationToken)
    {
        // Compose the RELion API call via IRelionService and return Result<T>
        return Result.Ok(Enumerable.Empty<RelionCompany>());
    }
}
```

Register the handler's assembly via `AddConsumerHandlers(...)` on the main IntegratoR builder (not on `AddRelionClient` — the consumer's handlers belong to the consumer's assembly, not RELion's).

## Authentication Handler

`RelionAuthenticationHandler` is a `DelegatingHandler` that runs on every outbound HTTP request. Depending on `AuthMode`:

- `ApiKey` — appends the `SubscriptionHeaderKey` header with `SubscriptionKey` as its value
- `OAuth` — acquires a bearer token via the configured OAuth credentials and adds an `Authorization: Bearer <token>` header

Tokens are not cached as aggressively as in the OData side — each request currently triggers a fresh acquisition. This is acceptable for the low-volume read pattern RELion is typically used for; high-volume scenarios should add a layer of caching at the consumer level.

## DTO and Domain Model Split

The module separates wire DTOs (under `Domain/DTOs/`) from domain models (under `Domain/Models/`):

| Layer | Examples | Purpose |
|---|---|---|
| DTO (wire) | `RelionRequest`, `RelionResponsePayload`, `RelionResponseEntity`, `RelionDataWrapper` | Matches the RELion API JSON shape exactly |
| Domain (model) | `RelionCompany`, `RelionLedgerAccountMapping`, `RelionLedgerJournalLine` | Strongly-typed, business-meaningful representations |

`IRelionService` and the MediatR handlers operate on domain models. The DTO layer is internal to the module and consumers should rarely need to touch it directly.

## Error Handling

The module returns the same `Result<T>` + `IntegrationError` shape as the rest of the framework. Failure codes follow the convention `RELion.*` (e.g. `RELion.RequestFailed`, `RELion.AuthenticationFailed`). See [Handle Errors](Handle-Errors).

## When Things Go Wrong

**Settings not bound** — the section name must be exactly `"RelionSettings"`. If the consumer renamed the section in `appsettings.json`, the typed binding silently yields a default instance and the first call fails with an empty URL.

**Mixed `AuthMode`** — the handler reads `AuthMode` at every request. Switching at runtime works, but consumers should be aware of the token-acquisition cost on each switch.

**Company-scoped 404** — most RELion endpoints filter implicitly by `Company`. A `404 NotFound` on a query that should return data often means the `Company` setting points at a tenant where the data does not exist. Verify the `Company` value matches RELion's expectations.

## See Also

- [Handle Errors](Handle-Errors) — `IntegrationError` and `Result<T>` shape
- [Authentication Modes](Authentication-Modes) — OAuth vs API Key fundamentals (the RELion handler follows the same pattern as the OData one)
- [Known Limitations](Known-Limitations) — RELion settings restructure is on the backlog
