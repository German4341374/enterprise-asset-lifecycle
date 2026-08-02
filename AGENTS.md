# Repository guidance

- Keep all source code, comments, documentation, and commit messages in English.
- Preserve the asset state machine and add tests for every transition change.
- Treat `AssetEvent` rows as append-only. Never introduce an update or delete path.
- Require the expected PostgreSQL `xmin` version for asset mutations.
- Keep assignment operations transactional and preserve the partial unique index for one active assignment.
- Never commit credentials, personal data, production inventory exports, or local `.env` files.
- Use EF Core migrations for schema changes and document rollback implications.
- Run formatting, build, unit tests, integration tests, and the container build when the environment supports them.

