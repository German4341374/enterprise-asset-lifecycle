# Security policy

## Reporting

Do not open a public issue for a vulnerability. Use GitHub private vulnerability reporting when enabled. Include a concise reproduction with synthetic data and remove credentials, employee information, asset serial numbers, and database exports.

## Supported version

Security fixes target the current `main` branch. Long-term support releases are not published.

## Security boundaries

The application validates request models, uses parameterized EF Core queries, restricts CSV size, neutralizes spreadsheet formulas during export, stores no application secrets in the repository, and runs its container as a non-root user. Audit events are append-only in both the application and PostgreSQL.

Authentication and authorization are intentionally out of scope. Never expose this demonstration directly to an untrusted network. Put it behind an identity-aware reverse proxy before any shared deployment.
