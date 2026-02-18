# Security

## Secrets Management

- Never hardcode secrets, connection strings, or credentials in source code
- Use vault services (Azure Key Vault, environment variables) for all sensitive configuration
- Never log secrets — be cautious with object destructuring in log statements

## Input Validation

- Validate all input at system boundaries (API endpoints, external data ingestion)
- Fail fast on invalid input — return clear error messages
- Do not trust data from external systems; validate schema and content

## Authentication & Tokens

- Use short-lived tokens with proactive refresh (refresh before expiry, not after failure)
- Cache tokens securely with expiration aligned to token lifetime
- Use client credentials flow for service-to-service communication

## Dependencies

- Pin dependency versions explicitly in project files
- Audit dependencies for known vulnerabilities regularly
- Prefer well-maintained, widely-used packages over niche alternatives
