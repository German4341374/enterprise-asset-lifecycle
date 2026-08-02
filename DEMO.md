# Five-minute demonstration

Prepare the environment before the meeting:

```bash
cp .env.example .env
# Set a local-only POSTGRES_PASSWORD.
docker compose up --build --detach
curl --fail http://localhost:8080/health/ready
```

## 0:00–0:45 — Frame the problem

Open <http://localhost:8080>. Explain that an operations team needs one trustworthy answer to three questions: where a device is, who held it, and which lifecycle decisions changed it. Point out the dashboard, readiness check, and intentionally small deployment shape.

## 0:45–1:30 — Register an asset

Open **Register**, create `AST-DEMO-01` with a clearly fictional serial, and choose Operations. The details page shows state `InStock`, an `xmin` version, and the first append-only event.

## 1:30–2:30 — Demonstrate controlled custody

Assign the asset to the seeded fictional employee Alex Morgan. Show that the operation creates the active assignment, changes the state to `Assigned`, moves custody to the employee's department, and appends one audit event in a single serializable transaction. Return it and show the historical events remain.

## 2:30–3:15 — Demonstrate the state machine

Start a repair, explain why an asset in repair cannot be assigned, then complete the repair. Mention that `Retired` is terminal and an assigned asset must be returned before repair or retirement.

## 3:15–4:00 — Demonstrate idempotency and concurrency

Open **CSV import**. Import a one-row sample with the generated key, then submit the identical file and key again. The second response is a replay, not a duplicate. Explain that changed bytes with the same key are rejected.

Describe a two-browser stale-write scenario: each mutation carries the displayed version, and PostgreSQL `xmin` makes the losing writer receive `409 CONCURRENCY_CONFLICT` instead of silently overwriting state.

## 4:00–5:00 — Show engineering evidence

Open the repository and point to:

- `AssetStateMachine` unit tests;
- Testcontainers tests using real PostgreSQL 18;
- the partial unique active-assignment index and append-only trigger in the migration;
- the non-root multi-stage Dockerfile and internal database network;
- GitHub Actions jobs for formatting, build, unit tests, PostgreSQL integration tests, coverage artifacts, and container build;
- `docs/operations.md` for import recovery, index rationale, conflict handling, and retention.

Finish with one trade-off: authentication is intentionally excluded, so this is a local portfolio environment until an identity-aware proxy and authorization model are added.

