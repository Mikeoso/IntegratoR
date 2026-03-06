# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Latest  | Yes       |

## Reporting a Vulnerability

If you discover a security vulnerability in IntegratoR, please report it responsibly.

**Do not open a public GitHub issue for security vulnerabilities.**

Instead, please use [GitHub's private vulnerability reporting](https://github.com/Mikeoso/IntegratoR/security/advisories/new) to submit your report.

### What to include

- A description of the vulnerability
- Steps to reproduce the issue
- The potential impact
- Any suggested fixes (optional)

### Response timeline

- **Acknowledgement**: Within 48 hours of your report
- **Assessment**: Within 7 days, we will assess the severity and impact
- **Fix**: Critical issues will be prioritized for the next release

## Security Best Practices

This project follows these security guidelines:

- No hardcoded secrets or credentials in source code
- Short-lived tokens with proactive refresh
- Input validation at all system boundaries
- Regular dependency vulnerability audits (`dotnet list package --vulnerable`)
