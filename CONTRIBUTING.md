# Contributing

Use a short-lived branch and Conventional Commits such as `feat: add warranty claim endpoint`, `fix: reject stale assignment version`, or `docs: explain import recovery`.

## Development workflow

1. Copy `.env.example` to `.env` and replace the local database password.
2. Run `make setup`.
3. Make a focused change with tests.
4. Run `make lint`, `make test-unit`, and the Testcontainers suite when Docker is available.
5. Explain database migrations, state transition changes, and rollback behavior in the pull request.

Do not commit credentials, production exports, serial numbers, employee data, connection strings, or Terraform/Kubernetes state. Test data must use reserved domains and clearly fictional values.

