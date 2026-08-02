# Operations decisions

## Concurrency conflicts

PostgreSQL updates the system column `xmin` whenever an asset row changes. EF Core maps it as the `Version` concurrency token. Every mutation requires the version returned by the preceding read.

If another writer commits first, EF Core's `UPDATE ... WHERE xmin = expected` affects zero rows and raises `DbUpdateConcurrencyException`. The API maps this to HTTP `409` and code `CONCURRENCY_CONFLICT`.

Operator recovery:

1. Fetch `GET /api/assets/{id}` and `/api/assets/{id}/events`.
2. Review the newer state and audit event.
3. Abandon the command if its premise is no longer true.
4. Otherwise submit a new command with the latest version.

Do not automatically retry a business command with a refreshed version; that could overwrite a deliberate custody or retirement decision.

## Import recovery

The import identity is `(Idempotency-Key, SHA-256(file bytes))`.

- Same key and same hash: return the stored result with `replayed=true`.
- Same key and different hash: reject with `IDEMPOTENCY_CONFLICT`.
- Existing asset tag or serial number: skip the row and count it.
- Invalid type, department, required value, or purchase date: record the row error and continue.
- Process failure before database commit: no batch or asset changes persist; retry with the same key.
- Response loss after commit: retry with the same key and receive the stored result.
- Concurrent first requests with one key: the unique index allows one commit; retry the losing request to retrieve the winner.

The importer does not retain the source file. Preserve the sanitized source and API result in the organization's approved evidence store if required. For files larger than 5 MB, split them with distinct keys or implement a staging-table workflow.

## Database indexes

| Table | Index | Purpose |
|---|---|---|
| `Assets` | unique `AssetTag` | Stable inventory identity |
| `Assets` | unique `SerialNumber` | Reject duplicate physical devices |
| `Assets` | `State` | Dashboard and state filters |
| `Assets` | `(DepartmentId, State)` | Department inventory views |
| `Assets` | `UpdatedAt` | Recent-change queries |
| `Assignments` | unique partial `AssetId` where `ReturnedAt IS NULL` | One active custodian |
| `Assignments` | `(AssetId, AssignedAt)` | Owner history |
| `Assignments` | `EmployeeId` | Employee equipment lookup |
| `MaintenanceRecords` | `(AssetId, Status)` | Open repair lookup |
| `Warranties` | unique `AssetId` | One current warranty record |
| `Warranties` | `EndDate` | Expiry scans |
| `SoftwareInstallations` | `(AssetId, Name)` | Device software inventory |
| `AssetEvents` | `(AssetId, OccurredAt)` | Ordered audit trail |
| `AssetEvents` | unique `DeduplicationKey` | Idempotent background events |
| `ImportBatches` | unique `IdempotencyKey` | Safe request replay |
| `ImportBatches` | `FileHash` | Operational investigation |

Indexes increase write cost and disk use. Validate additions with production-like `EXPLAIN (ANALYZE, BUFFERS)` evidence before merging them.

## Retention policy

The repository documents policy but does not schedule deletion. A production owner should configure and approve enforcement according to legal and business requirements.

| Data | Suggested retention | Reason |
|---|---:|---|
| Active assets, departments, employees | While active | Required for operations |
| Retired asset core record | 7 years after retirement | Ownership and disposal evidence |
| Assignments, maintenance, warranty, asset events | 7 years after retirement | Audit and support traceability |
| Software installations | Asset lifetime plus 2 years | License and investigation context |
| Import batch metadata and row errors | 90 days | Retry and reconciliation support |
| Application logs | 30 days | Troubleshooting with limited exposure |
| Source CSV | Not stored by this application | Reduce duplicate sensitive data |

Before deletion, verify there is no legal hold. Delete or pseudonymize employee contact data independently from immutable equipment events; events should use stable operator identifiers rather than copied personal details. Use database backups with matching expiry and test restoration before enforcing deletion.

## Backup and migration recovery

- Take a PostgreSQL backup before applying a schema migration.
- Apply migrations in a non-production environment with representative volume.
- Prefer a forward corrective migration. Never edit a migration already applied to a shared database.
- If startup migration fails, keep the new application out of rotation, inspect the EF migration history, restore if necessary, and deploy the last compatible image.
- Test both database restoration and application startup; a backup file alone is not recovery evidence.

