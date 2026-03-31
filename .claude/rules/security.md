# Security

## Secrets and Credentials

- No secrets in code, CLAUDE.md, or committed config files — ever.
- `local.settings.json` is gitignored — never commit it.
- Never read `.env` or `.env.*` files (denied in settings).
- OAuth credentials (`ClientSecret`) must be stored in Azure Key Vault for production.
- Subscription keys are sensitive — don't log them.

## Authentication

- `AuthenticationMode` must be explicitly set — don't rely on enum default.
- OAuth flow: `OAuthAuthenticator` acquires Bearer tokens via MSAL, caches with proactive refresh.
- ApiKey flow: subscription key added to HTTP header by `ODataAuthenticationHandler`.
- Auth handlers (`ODataAuthenticationHandler`, `RelionAuthenticationHandler`) are `DelegatingHandler` implementations — they run on every HTTP request.

## Error Responses

- `ReasonPhrase` in HTTP responses MUST NOT leak internal error details (tenant IDs, MSAL error codes).
- Use generic messages in HTTP responses; log full errors server-side only.
- Token acquisition failures return 401 — short-circuit the pipeline, don't call the downstream service.

## HTTP Headers

- `DefaultHeaders` dictionary is applied to APIM requests only — verify contents don't include auth headers.
- `Authorization` header is set by typed `AuthenticationHeaderValue("Bearer", token)` — prevents header injection.
